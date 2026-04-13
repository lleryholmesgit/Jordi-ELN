import SwiftUI

struct ScanResultPresentation: Identifiable {
    let id = UUID()
    let resolution: ScanResolution
}

struct ScanResultView: View {
    let resolution: ScanResolution
    let onDismiss: () -> Void
    let onOpenInventory: (Int) -> Void
    let onOpenELN: (Int) -> Void

    @EnvironmentObject private var store: AppStore
    @State private var isBusy = false
    @State private var message: String?
    @State private var quickAddInventory: InventoryItem?

    var body: some View {
        NavigationStack {
            List {
                switch resolution {
                case .inventory(let resolved):
                    inventorySection(resolved)
                case .storageLocation(let location):
                    storageLocationSection(location)
                }

                if let message {
                    Section {
                        Text(message)
                            .foregroundStyle(.secondary)
                    }
                }
            }
            .navigationTitle("Scan Result")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Close") {
                        onDismiss()
                    }
                }
            }
            .sheet(item: $quickAddInventory) { item in
                QuickAddELNView(
                    inventoryItem: item,
                    onCreated: { entry in
                        quickAddInventory = nil
                        onDismiss()
                        onOpenELN(entry.id)
                    }
                )
                .environmentObject(store)
            }
            .overlay {
                if isBusy {
                    ProgressView("Working...")
                        .padding(20)
                        .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 18, style: .continuous))
                }
            }
        }
    }

    @ViewBuilder
    private func inventorySection(_ resolved: ResolvedInventoryItem) -> some View {
        Section {
            VStack(alignment: .leading, spacing: 10) {
                Text(resolved.item.name)
                    .font(.title3.bold())
                Text(resolved.item.code)
                    .font(.subheadline.monospaced())
                    .foregroundStyle(.secondary)
                Text(resolved.item.location)
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
            .padding(.vertical, 4)
        }

        Section("Actions") {
            Button("View inventory item") {
                onDismiss()
                onOpenInventory(resolved.item.id)
            }

            if store.canUsePhoneScanActions {
                Button("Check in") {
                    performScanAction(successMessage: "Item checked in.") {
                        try await store.checkInInventoryItem(id: resolved.item.id)
                    }
                }

                Button("Check out") {
                    performScanAction(successMessage: "Item checked out.") {
                        try await store.checkOutInventoryItem(id: resolved.item.id)
                    }
                }
            }

            if canAddToELN {
                Button("Add to ELN") {
                    quickAddInventory = resolved.item
                }
            }
        }
    }

    @ViewBuilder
    private func storageLocationSection(_ location: ResolvedStorageLocation) -> some View {
        Section {
            VStack(alignment: .leading, spacing: 10) {
                Text(location.name)
                    .font(.title3.bold())
                Text(location.code)
                    .font(.subheadline.monospaced())
                    .foregroundStyle(.secondary)
                Text("\(location.inventoryItemCount) linked inventory item(s)")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                if !location.notes.isEmpty {
                    Text(location.notes)
                        .font(.body)
                }
            }
            .padding(.vertical, 4)
        } header: {
            Text("Storage Location")
        } footer: {
            Text("This QR resolves to a storage location. Inventory movement and assignment actions can use this location context.")
        }

        if store.canUseFullWorkspace, let detailURL = store.fullWorkspaceURL(path: location.detailPath) {
            Section("More") {
                Link("Open full workspace detail", destination: detailURL)
            }
        }
    }

    private var canAddToELN: Bool {
        if store.isPhoneClient {
            return store.authProfile?.canCreateOrEditELN ?? false
        }

        return store.canCreateOrEditELNOnCurrentDevice
    }

    private func performScanAction(successMessage: String, action: @escaping @Sendable () async throws -> Void) {
        isBusy = true
        message = nil

        Task {
            defer {
                isBusy = false
            }

            do {
                try await action()
                message = successMessage
            } catch {
                message = error.localizedDescription
            }
        }
    }
}

private struct QuickAddELNView: View {
    let inventoryItem: InventoryItem
    let onCreated: (ELNEntry) -> Void

    @Environment(\.dismiss) private var dismiss
    @EnvironmentObject private var store: AppStore

    @State private var title = ""
    @State private var experimentCode = ""
    @State private var projectName = ""
    @State private var principalInvestigator = ""
    @State private var notes = ""
    @State private var conductedOn = Date()
    @State private var isSaving = false
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            Form {
                Section("Record") {
                    TextField("Title", text: $title)
                    TextField("Experiment code", text: $experimentCode)
                    DatePicker("Date", selection: $conductedOn, displayedComponents: .date)
                    TextField("Project name", text: $projectName)
                    TextField("Technician / PI", text: $principalInvestigator)
                }

                Section("Linked inventory") {
                    Text(inventoryItem.name)
                    Text(inventoryItem.code)
                        .font(.subheadline.monospaced())
                        .foregroundStyle(.secondary)
                }

                Section("Notes") {
                    TextEditor(text: $notes)
                        .frame(minHeight: 160)
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle("Add to ELN")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") {
                        dismiss()
                    }
                }

                ToolbarItem(placement: .topBarTrailing) {
                    Button(isSaving ? "Saving..." : "Create") {
                        save()
                    }
                    .disabled(isSaving || title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
            .onAppear {
                if title.isEmpty {
                    title = inventoryItem.name
                }
            }
        }
    }

    private func save() {
        isSaving = true
        errorMessage = nil

        let payload = RecordSaveRequestPayload(
            title: title.trimmingCharacters(in: .whitespacesAndNewlines),
            experimentCode: experimentCode.trimmingCharacters(in: .whitespacesAndNewlines),
            conductedOn: Self.dateFormatter.string(from: conductedOn),
            projectName: projectName.nilIfBlank,
            principalInvestigator: principalInvestigator.nilIfBlank,
            templateId: nil,
            richTextContent: notes.nilIfBlank,
            structuredDataJson: "{}",
            tableJson: "{\"columns\":[],\"rows\":[]}",
            flowchartJson: "{\"nodes\":[],\"edges\":[]}",
            flowchartPreviewPath: nil,
            signatureStatement: nil,
            signatureDate: nil,
            instrumentLinks: [
                RecordInstrumentLinkPayload(instrumentId: inventoryItem.id, usageHours: nil)
            ]
        )

        Task {
            defer {
                isSaving = false
            }

            do {
                let entry = try await store.createELNEntry(request: payload)
                onCreated(entry)
            } catch {
                errorMessage = error.localizedDescription
            }
        }
    }

    private static let dateFormatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        return formatter
    }()
}

private extension String {
    var nilIfBlank: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}

