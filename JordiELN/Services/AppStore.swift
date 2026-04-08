import Foundation

@MainActor
final class AppStore: ObservableObject {
    @Published private(set) var inventoryItems: [InventoryItem] = []
    @Published private(set) var elnEntries: [ELNEntry] = []
    @Published private(set) var authProfile: UserProfile?
    @Published private(set) var inventoryErrorMessage: String?
    @Published private(set) var elnErrorMessage: String?
    @Published private(set) var isInventoryLoading = false
    @Published private(set) var isELNLoading = false
    @Published var isConnectionSettingsPresented = false
    @Published var serverBaseURL: String
    @Published var savedEmail: String
    @Published var sessionPassword = ""

    private let apiClient: APIClient
    private let defaults: UserDefaults

    private enum DefaultsKey {
        static let serverBaseURL = "jordi.serverBaseURL"
        static let savedEmail = "jordi.savedEmail"
    }

    init(
        apiClient: APIClient = APIClient(),
        defaults: UserDefaults = .standard
    ) {
        self.apiClient = apiClient
        self.defaults = defaults
        self.serverBaseURL = defaults.string(forKey: DefaultsKey.serverBaseURL) ?? ""
        self.savedEmail = defaults.string(forKey: DefaultsKey.savedEmail) ?? ""
    }

    var isAuthenticated: Bool {
        authProfile != nil
    }

    var hasSavedConnection: Bool {
        normalizedBaseURL() != nil
    }

    var hasSavedCredentials: Bool {
        !savedEmail.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    func inventoryItem(id: Int) -> InventoryItem? {
        inventoryItems.first { $0.id == id }
    }

    func elnEntry(id: Int) -> ELNEntry? {
        elnEntries.first { $0.id == id }
    }

    func linkedEntries(forInventoryID inventoryID: Int) -> [ELNEntry] {
        elnEntries.filter { entry in
            entry.instrumentLinks.contains { $0.instrumentId == inventoryID }
        }
    }

    func linkedInventory(forELNID elnID: Int) -> [InventoryItem] {
        guard let entry = elnEntry(id: elnID) else {
            return []
        }

        return entry.instrumentLinks.compactMap { link in
            link.instrument ?? inventoryItem(id: link.instrumentId)
        }
    }

    func persistConnectionSettings(baseURL: String, email: String) {
        let canonicalBaseURL = (try? validatedBaseURL(from: baseURL))?
            .absoluteString
            .trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        serverBaseURL = canonicalBaseURL ?? baseURL.trimmingCharacters(in: .whitespacesAndNewlines)
        savedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)
        defaults.set(serverBaseURL, forKey: DefaultsKey.serverBaseURL)
        defaults.set(savedEmail, forKey: DefaultsKey.savedEmail)

        clearLoadedData()
        authProfile = nil
        sessionPassword = ""

        if let baseURL = normalizedBaseURL() {
            Task {
                await apiClient.clearCookies(for: baseURL)
            }
        }
    }

    func configureAndSignIn(baseURL: String, email: String, password: String) async throws {
        let trimmedBaseURL = baseURL.trimmingCharacters(in: .whitespacesAndNewlines)
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)
        let trimmedPassword = password.trimmingCharacters(in: .whitespacesAndNewlines)

        guard !trimmedBaseURL.isEmpty else {
            throw APIClientError.missingConnectionConfiguration
        }

        guard !trimmedEmail.isEmpty, !trimmedPassword.isEmpty else {
            throw APIClientError.missingCredentials
        }

        let resolvedBaseURL = try validatedBaseURL(from: trimmedBaseURL)

        serverBaseURL = resolvedBaseURL.absoluteString.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        savedEmail = trimmedEmail
        defaults.set(serverBaseURL, forKey: DefaultsKey.serverBaseURL)
        defaults.set(savedEmail, forKey: DefaultsKey.savedEmail)

        clearLoadedData()
        authProfile = nil
        sessionPassword = trimmedPassword

        await apiClient.clearCookies(for: resolvedBaseURL)

        authProfile = try await apiClient.login(
            baseURL: resolvedBaseURL,
            email: trimmedEmail,
            password: trimmedPassword
        )
        await preloadAuthenticatedContent(baseURL: resolvedBaseURL)
    }

    func restoreProfileIfPossible() async {
        guard let baseURL = normalizedBaseURL() else {
            clearLoadedData()
            authProfile = nil
            return
        }

        do {
            authProfile = try await apiClient.fetchProfile(baseURL: baseURL)
        } catch {
            clearLoadedData()
            authProfile = nil
        }
    }

    func signIn() async throws {
        guard let baseURL = normalizedBaseURL() else {
            throw APIClientError.missingConnectionConfiguration
        }

        guard !savedEmail.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              !sessionPassword.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw APIClientError.missingCredentials
        }

        authProfile = try await apiClient.login(
            baseURL: baseURL,
            email: savedEmail,
            password: sessionPassword
        )
    }

    func signOut() async {
        guard let baseURL = normalizedBaseURL() else {
            clearLoadedData()
            authProfile = nil
            sessionPassword = ""
            return
        }

        do {
            try await apiClient.logout(baseURL: baseURL)
        } catch {
        }

        await apiClient.clearCookies(for: baseURL)
        clearLoadedData()
        authProfile = nil
        sessionPassword = ""
    }

    func loadInventory() async {
        guard let baseURL = normalizedBaseURL() else {
            inventoryErrorMessage = APIClientError.missingConnectionConfiguration.localizedDescription
            inventoryItems = []
            return
        }

        isInventoryLoading = true
        inventoryErrorMessage = nil

        defer {
            isInventoryLoading = false
        }

        do {
            try await ensureAuthenticated(for: baseURL)
            inventoryItems = try await apiClient.fetchInventory(baseURL: baseURL)
        } catch APIClientError.unauthorized {
            await handleUnauthorizedState(for: baseURL)
        } catch {
            inventoryItems = []
            inventoryErrorMessage = presentableError(error)
        }
    }

    func loadELN() async {
        guard let baseURL = normalizedBaseURL() else {
            elnErrorMessage = APIClientError.missingConnectionConfiguration.localizedDescription
            elnEntries = []
            return
        }

        isELNLoading = true
        elnErrorMessage = nil

        defer {
            isELNLoading = false
        }

        do {
            try await ensureAuthenticated(for: baseURL)
            let entries = try await apiClient.fetchRecords(baseURL: baseURL)
            elnEntries = entries
            upsertInventoryItems(from: entries)
        } catch APIClientError.unauthorized {
            await handleUnauthorizedState(for: baseURL)
        } catch {
            elnEntries = []
            elnErrorMessage = presentableError(error)
        }
    }

    func refreshInventoryItem(id: Int) async throws -> InventoryItem {
        guard let baseURL = normalizedBaseURL() else {
            throw APIClientError.missingConnectionConfiguration
        }

        do {
            try await ensureAuthenticated(for: baseURL)
            let item = try await apiClient.fetchInventoryItem(baseURL: baseURL, id: id)
            upsertInventoryItem(item)
            return item
        } catch APIClientError.unauthorized {
            await handleUnauthorizedState(for: baseURL)
            throw APIClientError.unauthorized
        }
    }

    func refreshELNEntry(id: Int) async throws -> ELNEntry {
        guard let baseURL = normalizedBaseURL() else {
            throw APIClientError.missingConnectionConfiguration
        }

        do {
            try await ensureAuthenticated(for: baseURL)
            let entry = try await apiClient.fetchRecord(baseURL: baseURL, id: id)
            upsertELNEntry(entry)
            upsertInventoryItems(from: [entry])
            return entry
        } catch APIClientError.unauthorized {
            await handleUnauthorizedState(for: baseURL)
            throw APIClientError.unauthorized
        }
    }

    func resolveInventoryPayload(_ payload: String) async throws -> InventoryItem {
        guard let baseURL = normalizedBaseURL() else {
            throw APIClientError.missingConnectionConfiguration
        }

        let resolved = try await apiClient.resolveQr(baseURL: baseURL, payload: payload)
        let summaryItem = InventoryItem.resolvedSummary(
            id: resolved.instrument.id,
            code: resolved.instrument.code,
            name: resolved.instrument.name,
            model: resolved.instrument.model,
            location: resolved.instrument.location,
            status: resolved.instrument.status
        )

        upsertInventoryItem(summaryItem)

        do {
            return try await refreshInventoryItem(id: resolved.instrument.id)
        } catch APIClientError.missingCredentials {
            return summaryItem
        } catch APIClientError.unauthorized {
            return summaryItem
        } catch {
            return summaryItem
        }
    }

    private func ensureAuthenticated(for baseURL: URL) async throws {
        if authProfile != nil {
            return
        }

        if let existingProfile = try? await apiClient.fetchProfile(baseURL: baseURL) {
            authProfile = existingProfile
            return
        }

        try await signIn()
    }

    private func clearLoadedData() {
        inventoryErrorMessage = nil
        elnErrorMessage = nil
        inventoryItems = []
        elnEntries = []
    }

    private func handleUnauthorizedState(for baseURL: URL) async {
        await apiClient.clearCookies(for: baseURL)
        clearLoadedData()
        authProfile = nil
        sessionPassword = ""
    }

    private func upsertELNEntry(_ entry: ELNEntry) {
        if let index = elnEntries.firstIndex(where: { $0.id == entry.id }) {
            elnEntries[index] = entry
        } else {
            elnEntries.append(entry)
        }
    }

    private func upsertInventoryItem(_ item: InventoryItem) {
        if let index = inventoryItems.firstIndex(where: { $0.id == item.id }) {
            if item.includesFullDetails || !inventoryItems[index].includesFullDetails {
                inventoryItems[index] = item
            }
        } else {
            inventoryItems.append(item)
        }
    }

    private func upsertInventoryItems(from entries: [ELNEntry]) {
        entries
            .flatMap(\.instrumentLinks)
            .compactMap(\.instrument)
            .forEach(upsertInventoryItem(_:))
    }

    private func preloadAuthenticatedContent(baseURL: URL) async {
        inventoryErrorMessage = nil
        elnErrorMessage = nil

        async let inventoryResult = preloadInventory(baseURL: baseURL)
        async let elnResult = preloadELN(baseURL: baseURL)

        let resolvedInventoryResult = await inventoryResult
        let resolvedELNResult = await elnResult

        switch resolvedInventoryResult {
        case .success(let items):
            inventoryItems = items
        case .failure(let error):
            inventoryItems = []
            inventoryErrorMessage = presentableError(error)
        }

        switch resolvedELNResult {
        case .success(let entries):
            elnEntries = entries
            upsertInventoryItems(from: entries)
        case .failure(let error):
            elnEntries = []
            elnErrorMessage = presentableError(error)
        }
    }

    private func preloadInventory(baseURL: URL) async -> Result<[InventoryItem], Error> {
        do {
            return .success(try await apiClient.fetchInventory(baseURL: baseURL))
        } catch {
            return .failure(error)
        }
    }

    private func preloadELN(baseURL: URL) async -> Result<[ELNEntry], Error> {
        do {
            return .success(try await apiClient.fetchRecords(baseURL: baseURL))
        } catch {
            return .failure(error)
        }
    }

    private func normalizedBaseURL() -> URL? {
        try? validatedBaseURL(from: serverBaseURL)
    }

    private func validatedBaseURL(from rawValue: String) throws -> URL {
        let trimmed = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            throw APIClientError.missingConnectionConfiguration
        }

        let sanitized = sanitizeServerURL(trimmed)
        guard var components = URLComponents(string: sanitized) else {
            throw APIClientError.invalidBaseURL
        }

        guard let scheme = components.scheme?.lowercased(),
              scheme == "http" || scheme == "https",
              let host = components.host,
              !host.isEmpty else {
            throw APIClientError.invalidBaseURL
        }

        if host == "0.0.0.0" {
            throw APIClientError.invalidServerAddress(
                "Do not use `0.0.0.0` in the app. Use `http://127.0.0.1:5050` in Simulator, or your Mac's LAN IP on a real iPhone."
            )
        }

        components.scheme = scheme
        components.path = ""
        components.query = nil
        components.fragment = nil

        guard let url = components.url else {
            throw APIClientError.invalidBaseURL
        }

        return url
    }

    private func sanitizeServerURL(_ rawValue: String) -> String {
        var value = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        value = value
            .replacingOccurrences(of: "：", with: ":")
            .replacingOccurrences(of: "／", with: "/")
            .replacingOccurrences(of: "。", with: ".")
            .replacingOccurrences(of: " ", with: "")

        if !value.contains("://") {
            value = "http://\(value)"
        }

        while value.hasSuffix("/") {
            value.removeLast()
        }

        return value
    }

    private func presentableError(_ error: Error) -> String {
        if let localizedError = error as? LocalizedError,
           let description = localizedError.errorDescription {
            return description
        }

        return error.localizedDescription
    }
}
