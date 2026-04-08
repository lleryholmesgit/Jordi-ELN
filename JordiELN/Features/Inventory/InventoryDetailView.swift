import SwiftUI

struct InventoryDetailScreen: View {
    let inventoryID: Int
    let linkedEntries: [ELNEntry]
    let onOpenELN: (Int) -> Void

    @EnvironmentObject private var store: AppStore
    @State private var item: InventoryItem?
    @State private var errorMessage: String?
    @State private var isLoading = false

    var body: some View {
        Group {
            if let item {
                InventoryDetailView(
                    item: item,
                    linkedEntries: linkedEntries,
                    onOpenELN: onOpenELN
                )
            } else if isLoading {
                ProgressView("Loading inventory detail...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(Color(.systemGroupedBackground))
            } else {
                ContentUnavailableView(
                    "Inventory not found",
                    systemImage: "shippingbox.badge.exclamationmark",
                    description: Text(errorMessage ?? "The inventory detail could not be loaded.")
                )
            }
        }
        .task(id: inventoryID) {
            await load()
        }
    }

    private func load() async {
        if let cachedItem = store.inventoryItem(id: inventoryID) {
            item = cachedItem
            if cachedItem.includesFullDetails {
                return
            }
        }

        isLoading = true
        defer {
            isLoading = false
        }

        do {
            item = try await store.refreshInventoryItem(id: inventoryID)
        } catch {
            errorMessage = error.localizedDescription
            if item == nil {
                item = store.inventoryItem(id: inventoryID)
            }
        }
    }
}

struct InventoryDetailView: View {
    let item: InventoryItem
    let linkedEntries: [ELNEntry]
    let onOpenELN: (Int) -> Void

    var body: some View {
        List {
            summarySection
            overviewSection
            specificsSection
            qrSection

            if !item.includesFullDetails {
                Section {
                    Label("This detail came from `/api/inventory/resolve-qr`. Sign in to load the full inventory record from `/api/inventory/{id}`.", systemImage: "info.circle")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
            }

            if !linkedEntries.isEmpty {
                Section("Linked ELN records") {
                    ForEach(linkedEntries) { entry in
                        Button {
                            onOpenELN(entry.id)
                        } label: {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(entry.title)
                                    .font(.headline)
                                    .foregroundStyle(.primary)
                                Text("\(entry.experimentCode) • \(entry.authorName)")
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                            }
                            .padding(.vertical, 4)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
        .navigationTitle("Inventory Detail")
        .navigationBarTitleDisplayMode(.inline)
        .listStyle(.insetGrouped)
    }

    private var summarySection: some View {
        Section {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(item.name)
                            .font(.title3.bold())
                        Text(item.code)
                            .font(.subheadline.monospaced())
                            .foregroundStyle(.secondary)
                    }

                    Spacer()
                    StatusBadge(title: item.status.title, color: badgeColor)
                }

                HStack(spacing: 8) {
                    StatusBadge(title: item.itemType.title, color: itemTypeColor)
                    if !item.manufacturer.isEmpty {
                        Text(item.manufacturer)
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }
                }

                if !item.notes.isEmpty {
                    Text(item.notes)
                        .font(.body)
                        .foregroundStyle(.secondary)
                }
            }
            .padding(.vertical, 6)
        }
    }

    private var overviewSection: some View {
        Section("Overview") {
            detailRow(label: "Type", value: item.itemType.title)
            detailRow(label: "Code", value: item.code)
            detailRow(label: "Location", value: item.location)
            detailRow(label: "Status", value: item.status.title)
            if let createdAtUtc = item.createdAtUtc {
                detailRow(label: "Created", value: createdAtUtc.formatted(date: .abbreviated, time: .shortened))
            }
        }
    }

    @ViewBuilder
    private var specificsSection: some View {
        switch item.itemType {
        case .instrument:
            Section("Instrument profile") {
                detailRow(label: "Model", value: item.model)
                detailRow(label: "Manufacturer", value: item.manufacturer)
                detailRow(label: "Serial number", value: item.serialNumber)
                detailRow(label: "Owner", value: item.ownerName)
                detailRow(label: "Calibration", value: item.calibrationInfo)
            }
        case .chemical:
            Section("Chemical profile") {
                detailRow(label: "Product number", value: item.productNumber)
                detailRow(label: "Cat number", value: item.catalogNumber)
                detailRow(label: "Lot number", value: item.lotNumber)
                detailRow(label: "Exp number", value: item.expNumber)
                detailRow(label: "Quantity", value: item.quantityText)
                detailRow(label: "Opened on", value: formattedDate(item.openedOn))
                detailRow(label: "Expiry date", value: formattedDate(item.expiresOn))
            }
        }
    }

    @ViewBuilder
    private var qrSection: some View {
        if !item.qrCodeToken.isEmpty {
            Section("QR identity") {
                Text(item.qrCodeToken)
                    .font(.subheadline.monospaced())
                    .textSelection(.enabled)
            }
        }
    }

    @ViewBuilder
    private func detailRow(label: String, value: String) -> some View {
        if !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            HStack(alignment: .top) {
                Text(label)
                    .foregroundStyle(.secondary)
                Spacer()
                Text(value)
                    .multilineTextAlignment(.trailing)
            }
        }
    }

    private func formattedDate(_ date: Date?) -> String {
        guard let date else {
            return ""
        }

        return date.formatted(date: .abbreviated, time: .omitted)
    }

    private var badgeColor: Color {
        switch item.status {
        case .active:
            return .green
        case .maintenance:
            return .orange
        case .retired:
            return .gray
        }
    }

    private var itemTypeColor: Color {
        switch item.itemType {
        case .instrument:
            return .blue
        case .chemical:
            return .brown
        }
    }
}
