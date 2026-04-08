import Foundation

struct UserProfile: Hashable, Codable {
    let id: String
    let email: String
    let displayName: String
    let roles: [String]
    let mobileAuthPlanned: Bool
}
