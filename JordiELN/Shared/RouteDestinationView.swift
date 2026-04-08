import SwiftUI

struct RouteDestinationView: View {
    let route: AppRoute
    let onOpenInventory: (Int) -> Void
    let onOpenELN: (Int) -> Void

    @EnvironmentObject private var store: AppStore

    var body: some View {
        switch route {
        case .inventory(let inventoryID):
            InventoryDetailScreen(
                inventoryID: inventoryID,
                linkedEntries: store.linkedEntries(forInventoryID: inventoryID),
                onOpenELN: onOpenELN
            )
        case .eln(let elnID):
            ELNDetailScreen(
                elnID: elnID,
                onOpenInventory: onOpenInventory,
                onOpenELN: onOpenELN
            )
        }
    }
}

private struct MissingRecordView: View {
    let title: String
    let message: String

    var body: some View {
        ContentUnavailableView(
            title,
            systemImage: "exclamationmark.magnifyingglass",
            description: Text(message)
        )
        .navigationBarTitleDisplayMode(.inline)
    }
}
