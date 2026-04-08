import Foundation

enum QRPayloadParser {
    static func inventoryIdentifier(from payload: String) -> String? {
        let trimmed = payload.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            return nil
        }

        if let directMatch = matchedInventoryID(in: trimmed) {
            return directMatch
        }

        if let url = URL(string: trimmed) {
            if let components = URLComponents(url: url, resolvingAgainstBaseURL: false) {
                let possibleKeys = ["inventory", "inventoryid", "inventory_id", "item", "id"]
                if let queryValue = components.queryItems?.first(where: { item in
                    possibleKeys.contains(item.name.lowercased())
                })?.value,
                   let queryMatch = matchedInventoryID(in: queryValue) {
                    return queryMatch
                }
            }

            let pathChunks = url.pathComponents
                .filter { $0 != "/" }
                .reversed()

            for component in pathChunks {
                if let pathMatch = matchedInventoryID(in: component) {
                    return pathMatch
                }
            }
        }

        let separators = CharacterSet(charactersIn: ":/")
        let rawTokens = trimmed.components(separatedBy: separators)
        for token in rawTokens.reversed() where !token.isEmpty {
            if let tokenMatch = matchedInventoryID(in: token) {
                return tokenMatch
            }
        }

        return nil
    }

    private static func matchedInventoryID(in text: String) -> String? {
        let pattern = #"INV-\d{4,}"#
        let uppercaseText = text.uppercased()
        guard let expression = try? NSRegularExpression(pattern: pattern) else {
            return nil
        }

        let range = NSRange(uppercaseText.startIndex..<uppercaseText.endIndex, in: uppercaseText)
        guard let match = expression.firstMatch(in: uppercaseText, options: [], range: range),
              let swiftRange = Range(match.range, in: uppercaseText) else {
            return nil
        }

        return String(uppercaseText[swiftRange])
    }
}
