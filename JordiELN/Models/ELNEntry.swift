import Foundation

enum ELNRecordStatus: Int, Hashable, CaseIterable, Codable {
    case draft = 0
    case submitted = 1
    case approved = 2
    case rejected = 3

    var title: String {
        switch self {
        case .draft:
            return "Draft"
        case .submitted:
            return "Submitted"
        case .approved:
            return "Approved"
        case .rejected:
            return "Rejected"
        }
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let rawValue = try? container.decode(Int.self),
           let status = ELNRecordStatus(rawValue: rawValue) {
            self = status
            return
        }

        let title = (try? container.decode(String.self).lowercased()) ?? ""
        switch title {
        case "draft":
            self = .draft
        case "submitted":
            self = .submitted
        case "approved":
            self = .approved
        case "rejected":
            self = .rejected
        default:
            self = .draft
        }
    }
}

struct ELNUser: Hashable, Codable {
    let id: String
    let displayName: String
    let email: String

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: DynamicCodingKey.self)
        id = container.decodeLossyString(forKeys: ["id", "userId", "createdByUserId"])
        displayName = container.decodeLossyString(forKeys: ["displayName", "userName", "name", "fullName"])
        email = container.decodeLossyString(forKeys: ["email", "emailAddress", "userEmail"])
    }
}

struct ELNAttachment: Identifiable, Hashable, Codable {
    let id: Int
    let fileName: String
    let contentType: String
    let length: Int64
    let isImage: Bool
    let uploadedAtUtc: Date?
    let uploadedByUserId: String

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: DynamicCodingKey.self)
        id = container.decodeLossyInt(forKeys: ["id", "attachmentId"])
        fileName = container.decodeLossyString(forKeys: ["fileName", "name", "storedFileName"])
        contentType = container.decodeLossyString(forKeys: ["contentType", "mimeType"], defaultValue: "application/octet-stream")
        length = container.decodeLossyInt64(forKeys: ["length", "size", "fileSize"])
        isImage = container.decodeLossyBool(forKeys: ["isImage", "image"])
        uploadedAtUtc = container.decodeLossyTimestamp(forKeys: ["uploadedAtUtc", "uploadedAt", "createdAtUtc"])
        uploadedByUserId = container.decodeLossyString(forKeys: ["uploadedByUserId", "createdByUserId", "userId"])
    }
}

struct ELNInstrumentLink: Hashable, Codable {
    let instrumentId: Int
    let instrument: InventoryItem?
    let linkedAtUtc: Date?
    let usageNote: String
    let usageHours: Decimal?

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: DynamicCodingKey.self)
        instrumentId = container.decodeLossyInt(forKeys: ["instrumentId", "inventoryId", "itemId"])
        instrument = try container.decodeLossyDecodable(InventoryItem.self, forKeys: ["instrument", "inventoryItem", "item"])
        linkedAtUtc = container.decodeLossyTimestamp(forKeys: ["linkedAtUtc", "createdAtUtc", "linkedAt"])
        usageNote = container.decodeLossyString(forKeys: ["usageNote", "note", "comment"])
        usageHours = container.decodeLossyDecimal(forKeys: ["usageHours", "hours"])
    }
}

struct ELNEntry: Identifiable, Hashable, Codable {
    let id: Int
    let title: String
    let experimentCode: String
    let conductedOn: Date?
    let projectName: String
    let principalInvestigator: String
    let richTextContent: String
    let structuredDataJson: String
    let tableJson: String
    let flowchartJson: String
    let status: ELNRecordStatus
    let reviewComment: String
    let signatureStatement: String
    let createdAtUtc: Date?
    let updatedAtUtc: Date?
    let createdByUser: ELNUser?
    let attachments: [ELNAttachment]
    let instrumentLinks: [ELNInstrumentLink]

    init(
        id: Int,
        title: String,
        experimentCode: String,
        conductedOn: Date?,
        projectName: String,
        principalInvestigator: String,
        richTextContent: String,
        structuredDataJson: String,
        tableJson: String,
        flowchartJson: String,
        status: ELNRecordStatus,
        reviewComment: String,
        signatureStatement: String,
        createdAtUtc: Date?,
        updatedAtUtc: Date?,
        createdByUser: ELNUser?,
        attachments: [ELNAttachment],
        instrumentLinks: [ELNInstrumentLink]
    ) {
        self.id = id
        self.title = title
        self.experimentCode = experimentCode
        self.conductedOn = conductedOn
        self.projectName = projectName
        self.principalInvestigator = principalInvestigator
        self.richTextContent = richTextContent
        self.structuredDataJson = structuredDataJson
        self.tableJson = tableJson
        self.flowchartJson = flowchartJson
        self.status = status
        self.reviewComment = reviewComment
        self.signatureStatement = signatureStatement
        self.createdAtUtc = createdAtUtc
        self.updatedAtUtc = updatedAtUtc
        self.createdByUser = createdByUser
        self.attachments = attachments
        self.instrumentLinks = instrumentLinks
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: DynamicCodingKey.self)

        id = container.decodeLossyInt(forKeys: ["id", "recordId"])
        title = container.decodeLossyString(forKeys: ["title", "name"])
        experimentCode = container.decodeLossyString(forKeys: ["experimentCode", "experiment_code", "experiment code", "code"])
        conductedOn = container.decodeLossyDateOnly(forKeys: ["conductedOn", "experimentDate", "date"])
        projectName = container.decodeLossyString(forKeys: ["projectName", "project"])
        principalInvestigator = container.decodeLossyString(
            forKeys: ["principalInvestigator", "principal investigator", "principallnvestigator", "pi"]
        )
        richTextContent = container.decodeLossyString(forKeys: ["richTextContent", "richText", "content", "notes", "description"])
        structuredDataJson = container.decodeLossyString(
            forKeys: ["structuredDataJson", "structuredData", "structuredJson"],
            defaultValue: "{}"
        )
        tableJson = container.decodeLossyString(
            forKeys: ["tableJson", "tableDataJson", "tableData"],
            defaultValue: "{\"columns\":[],\"rows\":[]}"
        )
        flowchartJson = container.decodeLossyString(
            forKeys: ["flowchartJson", "flowChartJson", "flowchartDataJson"],
            defaultValue: "{\"nodes\":[],\"edges\":[]}"
        )
        status = container.decodeLossyStatus(forKeys: ["status", "recordStatus"])
        reviewComment = container.decodeLossyString(forKeys: ["reviewComment", "comment", "reviewNotes"])
        signatureStatement = container.decodeLossyString(forKeys: ["signatureStatement", "signature", "signatureText"])
        createdAtUtc = container.decodeLossyTimestamp(forKeys: ["createdAtUtc", "createdAt", "createdOn"])
        updatedAtUtc = container.decodeLossyTimestamp(forKeys: ["updatedAtUtc", "updatedAt", "lastUpdatedAtUtc", "modifiedAt"])
        createdByUser = try container.decodeLossyDecodable(ELNUser.self, forKeys: ["createdByUser", "createdBy", "author", "owner"])
        attachments = container.decodeLossyArray(ELNAttachment.self, forKeys: ["attachments", "files"])
        instrumentLinks = container.decodeLossyArray(ELNInstrumentLink.self, forKeys: ["instrumentLinks", "linkedInstruments", "inventoryLinks"])
    }

    var authorName: String {
        if let displayName = createdByUser?.displayName, !displayName.isEmpty {
            return displayName
        }

        if let email = createdByUser?.email, !email.isEmpty {
            return email
        }

        if !principalInvestigator.isEmpty {
            return principalInvestigator
        }

        return "Unknown author"
    }

    var summaryText: String {
        let trimmed = richTextContent.plainTextSummary

        if !trimmed.isEmpty {
            return trimmed
        }

        if !projectName.isEmpty {
            return projectName
        }

        return "No summary available."
    }

    var linkedInventoryIDs: [Int] {
        instrumentLinks.map(\.instrumentId)
    }
}

private extension String {
    var plainTextSummary: String {
        guard !isEmpty else {
            return ""
        }

        var value = self
        let structuralPatterns: [(pattern: String, replacement: String)] = [
            ("(?i)<\\s*br\\s*/?>", "\n"),
            ("(?i)</\\s*(p|div|section|article|header|footer|li|ul|ol|table|thead|tbody|tfoot|tr|h[1-6])\\s*>", "\n"),
            ("(?i)</\\s*(td|th)\\s*>", " ")
        ]

        for item in structuralPatterns {
            value = value.replacingOccurrences(
                of: item.pattern,
                with: item.replacement,
                options: .regularExpression
            )
        }

        value = value.replacingOccurrences(
            of: "(?is)<\\s*(script|style)[^>]*>.*?<\\s*/\\s*(script|style)\\s*>",
            with: " ",
            options: .regularExpression
        )

        value = value.replacingOccurrences(
            of: "(?is)<[^>]+>",
            with: " ",
            options: .regularExpression
        )

        let entities: [String: String] = [
            "&nbsp;": " ",
            "&amp;": "&",
            "&lt;": "<",
            "&gt;": ">",
            "&quot;": "\"",
            "&#39;": "'",
            "&apos;": "'"
        ]

        for (entity, replacement) in entities {
            value = value.replacingOccurrences(of: entity, with: replacement)
        }

        return value
            .replacingOccurrences(of: "\\s+", with: " ", options: .regularExpression)
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }
}

private struct DynamicCodingKey: CodingKey {
    let stringValue: String
    let intValue: Int?

    init?(stringValue: String) {
        self.stringValue = stringValue
        intValue = nil
    }

    init?(intValue: Int) {
        stringValue = String(intValue)
        self.intValue = intValue
    }
}

private extension KeyedDecodingContainer where Key == DynamicCodingKey {
    func decodeLossyString(forKeys keys: [String], defaultValue: String = "") -> String {
        guard let key = matchingKey(for: keys) else {
            return defaultValue
        }

        if let value = try? decodeIfPresent(String.self, forKey: key) {
            return value
        }
        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return String(value)
        }
        if let value = try? decodeIfPresent(Int64.self, forKey: key) {
            return String(value)
        }
        if let value = try? decodeIfPresent(Double.self, forKey: key) {
            return String(value)
        }
        if let value = try? decodeIfPresent(Bool.self, forKey: key) {
            return String(value)
        }

        return defaultValue
    }

    func decodeLossyInt(forKeys keys: [String], defaultValue: Int = 0) -> Int {
        guard let key = matchingKey(for: keys) else {
            return defaultValue
        }

        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return value
        }
        if let value = try? decodeIfPresent(String.self, forKey: key),
           let parsed = Int(value) {
            return parsed
        }
        if let value = try? decodeIfPresent(Double.self, forKey: key) {
            return Int(value)
        }

        return defaultValue
    }

    func decodeLossyInt64(forKeys keys: [String], defaultValue: Int64 = 0) -> Int64 {
        guard let key = matchingKey(for: keys) else {
            return defaultValue
        }

        if let value = try? decodeIfPresent(Int64.self, forKey: key) {
            return value
        }
        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return Int64(value)
        }
        if let value = try? decodeIfPresent(String.self, forKey: key),
           let parsed = Int64(value) {
            return parsed
        }
        if let value = try? decodeIfPresent(Double.self, forKey: key) {
            return Int64(value)
        }

        return defaultValue
    }

    func decodeLossyBool(forKeys keys: [String], defaultValue: Bool = false) -> Bool {
        guard let key = matchingKey(for: keys) else {
            return defaultValue
        }

        if let value = try? decodeIfPresent(Bool.self, forKey: key) {
            return value
        }
        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return value != 0
        }
        if let value = try? decodeIfPresent(String.self, forKey: key) {
            switch value.lowercased() {
            case "true", "1", "yes":
                return true
            case "false", "0", "no":
                return false
            default:
                break
            }
        }

        return defaultValue
    }

    func decodeLossyDecimal(forKeys keys: [String]) -> Decimal? {
        guard let key = matchingKey(for: keys) else {
            return nil
        }

        if let value = try? decodeIfPresent(Decimal.self, forKey: key) {
            return value
        }
        if let value = try? decodeIfPresent(Double.self, forKey: key) {
            return Decimal(value)
        }
        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return Decimal(value)
        }
        if let value = try? decodeIfPresent(String.self, forKey: key),
           let parsed = Decimal(string: value) {
            return parsed
        }

        return nil
    }

    func decodeLossyDateOnly(forKeys keys: [String]) -> Date? {
        guard let key = matchingKey(for: keys) else {
            return nil
        }

        if let value = try? decodeIfPresent(String.self, forKey: key) {
            return InventoryDateFormatters.parseDateOnly(value) ?? InventoryDateFormatters.parseTimestamp(value)
        }
        if let value = try? decodeIfPresent(Double.self, forKey: key) {
            return InventoryDateFormatters.parseUnixTimestamp(value)
        }
        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return InventoryDateFormatters.parseUnixTimestamp(Double(value))
        }

        return nil
    }

    func decodeLossyTimestamp(forKeys keys: [String]) -> Date? {
        guard let key = matchingKey(for: keys) else {
            return nil
        }

        if let value = try? decodeIfPresent(String.self, forKey: key) {
            return InventoryDateFormatters.parseTimestamp(value)
        }
        if let value = try? decodeIfPresent(Double.self, forKey: key) {
            return InventoryDateFormatters.parseUnixTimestamp(value)
        }
        if let value = try? decodeIfPresent(Int64.self, forKey: key) {
            return InventoryDateFormatters.parseUnixTimestamp(Double(value))
        }
        if let value = try? decodeIfPresent(Int.self, forKey: key) {
            return InventoryDateFormatters.parseUnixTimestamp(Double(value))
        }

        return nil
    }

    func decodeLossyStatus(forKeys keys: [String]) -> ELNRecordStatus {
        guard let key = matchingKey(for: keys) else {
            return .draft
        }

        return (try? decodeIfPresent(ELNRecordStatus.self, forKey: key)) ?? .draft
    }

    func decodeLossyArray<T: Decodable>(_ type: T.Type, forKeys keys: [String]) -> [T] {
        guard let key = matchingKey(for: keys) else {
            return []
        }

        if let value = try? decodeIfPresent([T].self, forKey: key) {
            return value ?? []
        }

        if let wrappedContainer = try? nestedContainer(keyedBy: DynamicCodingKey.self, forKey: key),
           let valuesKey = wrappedContainer.matchingKey(for: ["$values", "items", "data", "value", "result"]),
           let value = try? wrappedContainer.decodeIfPresent([T].self, forKey: valuesKey) {
            return value ?? []
        }

        return []
    }

    func decodeLossyDecodable<T: Decodable>(_ type: T.Type, forKeys keys: [String]) throws -> T? {
        guard let key = matchingKey(for: keys) else {
            return nil
        }

        return try decodeIfPresent(T.self, forKey: key)
    }

    func matchingKey(for candidates: [String]) -> Key? {
        if let exactKey = allKeys.first(where: { key in
            candidates.contains(key.stringValue)
        }) {
            return exactKey
        }

        let normalizedCandidates = Set(candidates.map(normalizeDynamicKey))
        return allKeys.first { key in
            normalizedCandidates.contains(normalizeDynamicKey(key.stringValue))
        }
    }
}

private func normalizeDynamicKey(_ value: String) -> String {
    let scalarView = value.unicodeScalars.filter { scalar in
        CharacterSet.alphanumerics.contains(scalar)
    }
    return String(String.UnicodeScalarView(scalarView)).lowercased()
}
