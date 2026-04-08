import Foundation

enum APIClientError: LocalizedError {
    case invalidBaseURL
    case invalidServerAddress(String)
    case invalidResponse
    case unauthorized
    case missingConnectionConfiguration
    case missingCredentials
    case serverUnavailable(String)
    case serverMessage(String)

    var errorDescription: String? {
        switch self {
        case .invalidBaseURL:
            return "The server URL is invalid. Try `http://127.0.0.1:5050` in Simulator, or your Mac's LAN IP on a real iPhone."
        case .invalidServerAddress(let message):
            return message
        case .invalidResponse:
            return "The server returned an invalid response."
        case .unauthorized:
            return "This API requires a signed-in session."
        case .missingConnectionConfiguration:
            return "Configure the ELN server URL before loading inventory or ELN records."
        case .missingCredentials:
            return "Enter your email and password to browse inventory and ELN records."
        case .serverUnavailable(let message):
            return message
        case .serverMessage(let message):
            return message
        }
    }
}

actor APIClient {
    private let session: URLSession
    private let decoder: JSONDecoder

    init() {
        let configuration = URLSessionConfiguration.default
        configuration.httpShouldSetCookies = true
        configuration.httpCookieAcceptPolicy = .always
        configuration.httpCookieStorage = HTTPCookieStorage.shared
        session = URLSession(configuration: configuration)

        decoder = JSONDecoder()
    }

    func fetchProfile(baseURL: URL) async throws -> UserProfile {
        let request = try makeRequest(baseURL: baseURL, path: "/api/auth/me", method: "GET")
        return try await send(request, as: UserProfile.self)
    }

    func login(baseURL: URL, email: String, password: String, rememberMe: Bool = true) async throws -> UserProfile {
        let payload = LoginRequest(email: email, password: password, rememberMe: rememberMe)
        let request = try makeRequest(baseURL: baseURL, path: "/api/auth/login", method: "POST", jsonBody: payload)
        return try await send(request, as: UserProfile.self)
    }

    func logout(baseURL: URL) async throws {
        let request = try makeRequest(baseURL: baseURL, path: "/api/auth/logout", method: "POST")
        let _: LogoutResponse = try await send(request, as: LogoutResponse.self)
    }

    func fetchInventory(baseURL: URL) async throws -> [InventoryItem] {
        let request = try makeRequest(baseURL: baseURL, path: "/api/inventory", method: "GET")
        return try await send(request, as: [InventoryItem].self)
    }

    func fetchInventoryItem(baseURL: URL, id: Int) async throws -> InventoryItem {
        let request = try makeRequest(baseURL: baseURL, path: "/api/inventory/\(id)", method: "GET")
        return try await send(request, as: InventoryItem.self)
    }

    func resolveQr(baseURL: URL, payload: String) async throws -> InventoryResolveResponse {
        let request = try makeRequest(
            baseURL: baseURL,
            path: "/api/inventory/resolve-qr",
            method: "POST",
            jsonBody: ResolveQrRequest(qrPayload: payload)
        )
        return try await send(request, as: InventoryResolveResponse.self)
    }

    func fetchRecords(baseURL: URL) async throws -> [ELNEntry] {
        let request = try makeRequest(baseURL: baseURL, path: "/api/records", method: "GET")
        return try await sendLossyArray(request, as: ELNEntry.self)
    }

    func fetchRecord(baseURL: URL, id: Int) async throws -> ELNEntry {
        let request = try makeRequest(baseURL: baseURL, path: "/api/records/\(id)", method: "GET")
        return try await sendWrappedObject(request, as: ELNEntry.self)
    }

    func clearCookies(for baseURL: URL) {
        HTTPCookieStorage.shared.cookies(for: baseURL)?.forEach { cookie in
            HTTPCookieStorage.shared.deleteCookie(cookie)
        }
    }

    private func makeRequest<Body: Encodable>(
        baseURL: URL,
        path: String,
        method: String,
        jsonBody: Body? = nil
    ) throws -> URLRequest {
        try validateBaseURL(baseURL)

        guard let url = URL(string: path, relativeTo: baseURL)?.absoluteURL else {
            throw APIClientError.invalidBaseURL
        }

        var request = URLRequest(url: url)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        if let jsonBody {
            request.httpBody = try JSONEncoder().encode(jsonBody)
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }

        return request
    }

    private func makeRequest(
        baseURL: URL,
        path: String,
        method: String
    ) throws -> URLRequest {
        try makeRequest(baseURL: baseURL, path: path, method: method, jsonBody: Optional<String>.none)
    }

    private func send<Response: Decodable>(_ request: URLRequest, as type: Response.Type) async throws -> Response {
        let data = try await sendData(request)
        return try decoder.decode(Response.self, from: data)
    }

    private func sendLossyArray<Element: Decodable>(_ request: URLRequest, as type: Element.Type) async throws -> [Element] {
        let data = try await sendData(request)
        let rawObject = try parseJSONObject(from: data, request: request)
        let rawItems = try extractJSONArray(from: rawObject)

        return rawItems.compactMap { rawItem in
            guard let itemData = encodedJSONObjectData(from: rawItem) else {
                return nil
            }

            return try? decoder.decode(Element.self, from: itemData)
        }
    }

    private func sendWrappedObject<Element: Decodable>(_ request: URLRequest, as type: Element.Type) async throws -> Element {
        let data = try await sendData(request)

        if let decoded = try? decoder.decode(Element.self, from: data) {
            return decoded
        }

        let rawObject = try parseJSONObject(from: data, request: request)
        let wrappedObject = try extractJSONObject(from: rawObject)

        guard let wrappedData = encodedJSONObjectData(from: wrappedObject) else {
            throw APIClientError.invalidResponse
        }

        return try decoder.decode(Element.self, from: wrappedData)
    }

    private func sendData(_ request: URLRequest) async throws -> Data {
        let data: Data
        let response: URLResponse

        do {
            (data, response) = try await session.data(for: request)
        } catch {
            throw mapTransportError(error, request: request)
        }

        guard let httpResponse = response as? HTTPURLResponse else {
            throw APIClientError.invalidResponse
        }

        switch httpResponse.statusCode {
        case 200 ..< 300:
            return data
        case 401:
            if let errorEnvelope = try? decoder.decode(ServerErrorEnvelope.self, from: data),
               let message = errorEnvelope.message,
               !message.isEmpty {
                throw APIClientError.serverMessage(message)
            }
            throw APIClientError.unauthorized
        default:
            if let errorEnvelope = try? decoder.decode(ServerErrorEnvelope.self, from: data),
               let message = errorEnvelope.message,
               !message.isEmpty {
                throw APIClientError.serverMessage(message)
            }

            throw APIClientError.serverMessage("Request failed with status \(httpResponse.statusCode).")
        }
    }

    private func parseJSONObject(from data: Data, request: URLRequest) throws -> Any {
        let rawObject: Any

        do {
            rawObject = try JSONSerialization.jsonObject(with: data)
        } catch {
            if let repairedData = repairedJSONData(from: data),
               let repairedObject = try? JSONSerialization.jsonObject(with: repairedData) {
                return repairedObject
            }

            if let text = String(data: data, encoding: .utf8)?
                .trimmingCharacters(in: .whitespacesAndNewlines),
               !text.isEmpty {
                let snippet = String(text.prefix(160))
                let path = request.url?.path ?? "response"
                throw APIClientError.serverMessage("`\(path)` returned non-JSON content: \(snippet)")
            }

            throw error
        }

        if let string = rawObject as? String,
           let nestedData = string.data(using: .utf8) {
            do {
                return try JSONSerialization.jsonObject(with: nestedData)
            } catch {
                if let repairedData = repairedJSONData(from: nestedData),
                   let repairedObject = try? JSONSerialization.jsonObject(with: repairedData) {
                    return repairedObject
                }

                let snippet = String(string.prefix(160))
                let path = request.url?.path ?? "response"
                throw APIClientError.serverMessage("`\(path)` returned text instead of JSON: \(snippet)")
            }
        }

        return rawObject
    }

    private func extractJSONArray(from rawObject: Any) throws -> [Any] {
        if let array = rawObject as? [Any] {
            return array
        }

        guard let dictionary = rawObject as? [String: Any] else {
            throw APIClientError.invalidResponse
        }

        let preferredKeys = ["items", "records", "data", "value", "result", "$values"]
        for key in preferredKeys {
            if let array = dictionary[key] as? [Any] {
                return array
            }
            if let nested = dictionary[key] {
                if let array = try? extractJSONArray(from: nested) {
                    return array
                }
            }
        }

        if let firstArray = dictionary.values.first(where: { $0 is [Any] }) as? [Any] {
            return firstArray
        }

        throw APIClientError.invalidResponse
    }

    private func extractJSONObject(from rawObject: Any) throws -> Any {
        guard let dictionary = rawObject as? [String: Any] else {
            throw APIClientError.invalidResponse
        }

        let preferredKeys = ["item", "record", "data", "value", "result"]
        for key in preferredKeys {
            if let nested = dictionary[key] {
                return nested
            }
        }

        return dictionary
    }

    private func encodedJSONObjectData(from rawObject: Any) -> Data? {
        if JSONSerialization.isValidJSONObject(rawObject) {
            return try? JSONSerialization.data(withJSONObject: rawObject)
        }

        if let string = rawObject as? String {
            return string.data(using: .utf8)
        }

        return nil
    }

    private func repairedJSONData(from data: Data) -> Data? {
        guard let text = String(data: data, encoding: .utf8) else {
            return nil
        }

        return sanitizeJSONString(text).data(using: .utf8)
    }

    private func sanitizeJSONString(_ text: String) -> String {
        var result = ""
        result.reserveCapacity(text.count)

        var isInsideString = false
        var isEscaping = false

        for character in text {
            if isEscaping {
                result.append(character)
                isEscaping = false
                continue
            }

            if character == "\\" {
                result.append(character)
                isEscaping = true
                continue
            }

            if character == "\"" {
                result.append(character)
                isInsideString.toggle()
                continue
            }

            if isInsideString {
                switch character {
                case "\n":
                    result.append("\\n")
                case "\r":
                    result.append("\\r")
                case "\t":
                    result.append("\\t")
                case "\u{08}":
                    result.append("\\b")
                case "\u{0C}":
                    result.append("\\f")
                default:
                    result.append(character)
                }
            } else {
                result.append(character)
            }
        }

        return result
    }

    private func validateBaseURL(_ baseURL: URL) throws {
        guard let host = baseURL.host?.lowercased(), !host.isEmpty else {
            throw APIClientError.invalidBaseURL
        }

        if host == "0.0.0.0" {
            throw APIClientError.invalidServerAddress(
                "Do not use `0.0.0.0` in the app. Use `http://127.0.0.1:5050` in Simulator, or your Mac's LAN IP on a real iPhone."
            )
        }
    }

    private func mapTransportError(_ error: Error, request: URLRequest) -> APIClientError {
        guard let urlError = error as? URLError else {
            return .serverUnavailable(error.localizedDescription)
        }

        let host = request.url?.host?.lowercased() ?? "server"

        switch urlError.code {
        case .cannotFindHost, .dnsLookupFailed:
            return .serverUnavailable("Cannot find `\(host)`. Check the server address and port.")
        case .cannotConnectToHost, .networkConnectionLost, .notConnectedToInternet:
            if host == "127.0.0.1" || host == "localhost" {
                #if targetEnvironment(simulator)
                return .serverUnavailable("Cannot reach `\(host)`. Make sure `./run-app.sh` is running on this Mac.")
                #else
                return .serverUnavailable("`127.0.0.1` and `localhost` only work in Simulator. On a real iPhone, use your Mac's LAN IP like `http://192.168.x.x:5050`.")
                #endif
            }

            return .serverUnavailable("Cannot reach `\(host)`. Make sure the ASP.NET server is running and the address is correct.")
        case .timedOut:
            return .serverUnavailable("The server at `\(host)` timed out. Make sure `./run-app.sh` is still running.")
        default:
            return .serverUnavailable(urlError.localizedDescription)
        }
    }
}

private struct LoginRequest: Encodable {
    let email: String
    let password: String
    let rememberMe: Bool
}

struct InventoryResolveResponse: Decodable {
    let success: Bool
    let message: String
    let instrument: ResolvedInventorySummary
}

struct ResolvedInventorySummary: Decodable {
    let id: Int
    let code: String
    let name: String
    let model: String
    let location: String
    let status: InventoryStatus
}

private struct ResolveQrRequest: Encodable {
    let qrPayload: String
}

private struct ServerErrorEnvelope: Decodable {
    let message: String?
}

private struct LogoutResponse: Decodable {
    let message: String
}
