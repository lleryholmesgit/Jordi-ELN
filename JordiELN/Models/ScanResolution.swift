import Foundation

enum ScanResolution: Hashable {
    case inventory(ResolvedInventoryItem)
    case storageLocation(ResolvedStorageLocation)
}

struct ResolvedInventoryItem: Hashable {
    let summary: ResolvedInventorySummary
    let item: InventoryItem
}

struct ResolvedStorageLocation: Hashable, Codable {
    let id: Int
    let code: String
    let name: String
    let notes: String
    let qrCodeToken: String
    let inventoryItemCount: Int
    let detailPath: String
}

struct InventoryScanActions: Hashable, Codable {
    let viewPath: String?
    let scanOptionsPath: String?
    let addToElnPath: String?
}

struct ResolvedInventorySummary: Hashable, Codable {
    let id: Int
    let code: String
    let name: String
    let model: String
    let location: String
    let storageLocationId: Int?
    let status: InventoryStatus
    let actions: InventoryScanActions?
}

