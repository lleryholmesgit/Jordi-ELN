import Foundation

enum InventoryItemType: Int, Hashable, CaseIterable, Codable {
    case instrument = 0
    case chemical = 1
    case extract = 2
    case consumable = 3
    case officeSupply = 4

    var title: String {
        switch self {
        case .instrument:
            return "Instrument"
        case .chemical:
            return "Chemical"
        case .extract:
            return "Extract"
        case .consumable:
            return "Consumable"
        case .officeSupply:
            return "Office Supply"
        }
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let rawValue = try? container.decode(Int.self),
           let itemType = InventoryItemType(rawValue: rawValue) {
            self = itemType
            return
        }

        let title = try container.decode(String.self).lowercased()
        switch title {
        case "instrument":
            self = .instrument
        case "chemical":
            self = .chemical
        case "extract":
            self = .extract
        case "consumable":
            self = .consumable
        case "officesupply", "office supply":
            self = .officeSupply
        default:
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Unsupported inventory item type: \(title)")
        }
    }
}

enum InventoryStatus: Int, Hashable, CaseIterable, Codable {
    case active = 0
    case maintenance = 1
    case retired = 2

    var title: String {
        switch self {
        case .active:
            return "Active"
        case .maintenance:
            return "Maintenance"
        case .retired:
            return "Retired"
        }
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let rawValue = try? container.decode(Int.self),
           let status = InventoryStatus(rawValue: rawValue) {
            self = status
            return
        }

        let title = try container.decode(String.self).lowercased()
        switch title {
        case "active":
            self = .active
        case "maintenance":
            self = .maintenance
        case "retired":
            self = .retired
        default:
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Unsupported inventory status: \(title)")
        }
    }
}

struct InventoryItem: Identifiable, Hashable, Codable {
    let id: Int
    let itemType: InventoryItemType
    let code: String
    let name: String
    let model: String
    let manufacturer: String
    let serialNumber: String
    let location: String
    let storageLocationId: Int?
    let status: InventoryStatus
    let ownerName: String
    let calibrationInfo: String
    let productNumber: String
    let catalogNumber: String
    let lotNumber: String
    let expNumber: String
    let quantity: Decimal?
    let unit: String
    let openedOn: Date?
    let expiresOn: Date?
    let notes: String
    let qrCodeToken: String
    let createdAtUtc: Date?
    let includesFullDetails: Bool

    private enum CodingKeys: String, CodingKey {
        case id
        case itemType
        case code
        case name
        case model
        case manufacturer
        case serialNumber
        case location
        case storageLocationId
        case status
        case ownerName
        case calibrationInfo
        case productNumber
        case catalogNumber
        case lotNumber
        case expNumber
        case quantity
        case unit
        case openedOn
        case expiresOn
        case notes
        case qrCodeToken
        case createdAtUtc
    }

    init(
        id: Int,
        itemType: InventoryItemType,
        code: String,
        name: String,
        model: String,
        manufacturer: String,
        serialNumber: String,
        location: String,
        storageLocationId: Int?,
        status: InventoryStatus,
        ownerName: String,
        calibrationInfo: String,
        productNumber: String,
        catalogNumber: String,
        lotNumber: String,
        expNumber: String,
        quantity: Decimal?,
        unit: String,
        openedOn: Date?,
        expiresOn: Date?,
        notes: String,
        qrCodeToken: String,
        createdAtUtc: Date?,
        includesFullDetails: Bool
    ) {
        self.id = id
        self.itemType = itemType
        self.code = code
        self.name = name
        self.model = model
        self.manufacturer = manufacturer
        self.serialNumber = serialNumber
        self.location = location
        self.storageLocationId = storageLocationId
        self.status = status
        self.ownerName = ownerName
        self.calibrationInfo = calibrationInfo
        self.productNumber = productNumber
        self.catalogNumber = catalogNumber
        self.lotNumber = lotNumber
        self.expNumber = expNumber
        self.quantity = quantity
        self.unit = unit
        self.openedOn = openedOn
        self.expiresOn = expiresOn
        self.notes = notes
        self.qrCodeToken = qrCodeToken
        self.createdAtUtc = createdAtUtc
        self.includesFullDetails = includesFullDetails
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(Int.self, forKey: .id)
        itemType = try container.decode(InventoryItemType.self, forKey: .itemType)
        code = try container.decode(String.self, forKey: .code)
        name = try container.decode(String.self, forKey: .name)
        model = try container.decodeIfPresent(String.self, forKey: .model) ?? ""
        manufacturer = try container.decodeIfPresent(String.self, forKey: .manufacturer) ?? ""
        serialNumber = try container.decodeIfPresent(String.self, forKey: .serialNumber) ?? ""
        location = try container.decodeIfPresent(String.self, forKey: .location) ?? ""
        storageLocationId = try container.decodeIfPresent(Int.self, forKey: .storageLocationId)
        status = try container.decode(InventoryStatus.self, forKey: .status)
        ownerName = try container.decodeIfPresent(String.self, forKey: .ownerName) ?? ""
        calibrationInfo = try container.decodeIfPresent(String.self, forKey: .calibrationInfo) ?? ""
        productNumber = try container.decodeIfPresent(String.self, forKey: .productNumber) ?? ""
        catalogNumber = try container.decodeIfPresent(String.self, forKey: .catalogNumber) ?? ""
        lotNumber = try container.decodeIfPresent(String.self, forKey: .lotNumber) ?? ""
        expNumber = try container.decodeIfPresent(String.self, forKey: .expNumber) ?? ""
        quantity = try container.decodeIfPresent(Decimal.self, forKey: .quantity)
        unit = try container.decodeIfPresent(String.self, forKey: .unit) ?? ""
        openedOn = try Self.decodeDateOnly(container, forKey: .openedOn)
        expiresOn = try Self.decodeDateOnly(container, forKey: .expiresOn)
        notes = try container.decodeIfPresent(String.self, forKey: .notes) ?? ""
        qrCodeToken = try container.decodeIfPresent(String.self, forKey: .qrCodeToken) ?? ""
        createdAtUtc = try Self.decodeTimestamp(container, forKey: .createdAtUtc)
        includesFullDetails = true
    }

    private static func decodeDateOnly(
        _ container: KeyedDecodingContainer<CodingKeys>,
        forKey key: CodingKeys
    ) throws -> Date? {
        guard let rawValue = try container.decodeIfPresent(String.self, forKey: key) else {
            return nil
        }

        return InventoryDateFormatters.parseDateOnly(rawValue)
    }

    private static func decodeTimestamp(
        _ container: KeyedDecodingContainer<CodingKeys>,
        forKey key: CodingKeys
    ) throws -> Date? {
        guard let rawValue = try container.decodeIfPresent(String.self, forKey: key) else {
            return nil
        }

        return InventoryDateFormatters.parseTimestamp(rawValue)
    }

    var subtitle: String {
        "\(code) • \(location)"
    }

    var primaryMetadata: String {
        switch itemType {
        case .instrument:
            return model.isEmpty ? status.title : "\(model) • \(status.title)"
        case .chemical:
            if let quantity {
                return "\(NSDecimalNumber(decimal: quantity).stringValue) \(unit) • \(status.title)".trimmingCharacters(in: .whitespaces)
            }
            return unit.isEmpty ? status.title : "\(unit) • \(status.title)"
        case .extract, .consumable, .officeSupply:
            if let quantity {
                return "\(NSDecimalNumber(decimal: quantity).stringValue) \(unit) • \(status.title)".trimmingCharacters(in: .whitespaces)
            }
            return manufacturer.isEmpty ? status.title : "\(manufacturer) • \(status.title)"
        }
    }

    var quantityText: String {
        guard let quantity else {
            return unit
        }

        return "\(NSDecimalNumber(decimal: quantity).stringValue) \(unit)".trimmingCharacters(in: .whitespaces)
    }
}

extension InventoryItem {
    static func resolvedSummary(
        id: Int,
        code: String,
        name: String,
        model: String,
        location: String,
        storageLocationId: Int?,
        status: InventoryStatus
    ) -> InventoryItem {
        InventoryItem(
            id: id,
            itemType: .instrument,
            code: code,
            name: name,
            model: model,
            manufacturer: "",
            serialNumber: "",
            location: location,
            storageLocationId: storageLocationId,
            status: status,
            ownerName: "",
            calibrationInfo: "",
            productNumber: "",
            catalogNumber: "",
            lotNumber: "",
            expNumber: "",
            quantity: nil,
            unit: "",
            openedOn: nil,
            expiresOn: nil,
            notes: "",
            qrCodeToken: "",
            createdAtUtc: nil,
            includesFullDetails: false
        )
    }
}

enum InventoryDateFormatters {
    static func parseDateOnly(_ value: String) -> Date? {
        if let date = parseTimestamp(value) {
            return date
        }

        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        if let date = formatter.date(from: value) {
            return date
        }

        formatter.dateFormat = "yyyy/MM/dd"
        if let date = formatter.date(from: value) {
            return date
        }

        formatter.dateFormat = "M/d/yyyy"
        return formatter.date(from: value)
    }

    static func parseTimestamp(_ value: String) -> Date? {
        if let unixValue = Double(value) {
            return parseUnixTimestamp(unixValue)
        }

        let fractionalFormatter = ISO8601DateFormatter()
        fractionalFormatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let date = fractionalFormatter.date(from: value) {
            return date
        }

        let plainFormatter = ISO8601DateFormatter()
        plainFormatter.formatOptions = [.withInternetDateTime]
        if let date = plainFormatter.date(from: value) {
            return date
        }

        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd HH:mm:ss"
        if let date = formatter.date(from: value) {
            return date
        }

        formatter.dateFormat = "M/d/yyyy h:mm:ss a"
        return formatter.date(from: value)
    }

    static func parseUnixTimestamp(_ value: Double) -> Date {
        let normalizedValue = value > 1_000_000_000_000 ? value / 1000 : value
        return Date(timeIntervalSince1970: normalizedValue)
    }
}
