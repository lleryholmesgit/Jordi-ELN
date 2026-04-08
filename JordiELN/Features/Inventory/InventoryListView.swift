import SwiftUI

struct InventoryListView: View {
    let onSelectInventory: (Int) -> Void

    @EnvironmentObject private var store: AppStore
    @State private var searchText = ""

    private var filteredItems: [InventoryItem] {
        guard !searchText.isEmpty else {
            return store.inventoryItems
        }

        let query = searchText.localizedLowercase
        return store.inventoryItems.filter { item in
            item.code.localizedLowercase.contains(query) ||
            item.name.localizedLowercase.contains(query) ||
            item.location.localizedLowercase.contains(query) ||
            item.manufacturer.localizedLowercase.contains(query) ||
            item.itemType.title.localizedLowercase.contains(query)
        }
    }

    var body: some View {
        List {
            if let errorMessage = store.inventoryErrorMessage, store.inventoryItems.isEmpty {
                Section {
                    ContentUnavailableView(
                        "Inventory unavailable",
                        systemImage: "externaldrive.badge.exclamationmark",
                        description: Text(errorMessage)
                    )

                    Button("Configure connection") {
                        store.isConnectionSettingsPresented = true
                    }
                }
            } else if store.isInventoryLoading && store.inventoryItems.isEmpty {
                Section {
                    HStack {
                        Spacer()
                        ProgressView("Loading inventory...")
                        Spacer()
                    }
                }
            } else {
                Section {
                    Text("Inventory now loads from `/api/inventory`. Mobile remains read-only even after sign-in.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }

                Section("Inventory items") {
                    ForEach(filteredItems) { item in
                        Button {
                            onSelectInventory(item.id)
                        } label: {
                            InventoryRow(item: item)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
        .navigationTitle("Inventory")
        .listStyle(.insetGrouped)
        .searchable(text: $searchText, prompt: "Search inventory")
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button {
                    store.isConnectionSettingsPresented = true
                } label: {
                    Image(systemName: "server.rack")
                }

                Button {
                    Task {
                        await store.loadInventory()
                    }
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .disabled(store.isInventoryLoading)
            }
        }
        .task {
            if store.inventoryItems.isEmpty {
                await store.loadInventory()
            }
        }
        .refreshable {
            await store.loadInventory()
        }
    }
}

private struct InventoryRow: View {
    let item: InventoryItem

    var body: some View {
        HStack(spacing: 12) {
            RoundedRectangle(cornerRadius: 14, style: .continuous)
                .fill(Color(red: 0.04, green: 0.43, blue: 0.32).opacity(0.12))
                .frame(width: 46, height: 46)
                .overlay {
                    Image(systemName: "shippingbox.fill")
                        .foregroundStyle(Color(red: 0.04, green: 0.43, blue: 0.32))
                }

            VStack(alignment: .leading, spacing: 4) {
                Text(item.name)
                    .font(.headline)
                    .foregroundStyle(.primary)
                Text("\(item.code) • \(item.location)")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                Text(item.itemType.title)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
            }

            Spacer()
            VStack(alignment: .trailing, spacing: 6) {
                StatusBadge(title: item.status.title, color: badgeColor)
                Text(item.primaryMetadata)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.trailing)
            }
        }
        .padding(.vertical, 4)
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
}
