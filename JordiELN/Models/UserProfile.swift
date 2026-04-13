import Foundation
import UIKit

enum MobileDeviceKind: String, Codable, Hashable {
    case phone
    case tablet

    @MainActor
    static var current: MobileDeviceKind {
        switch UIDevice.current.userInterfaceIdiom {
        case .pad:
            return .tablet
        default:
            return .phone
        }
    }

    var title: String {
        switch self {
        case .phone:
            return "iPhone"
        case .tablet:
            return "iPad"
        }
    }

    var apiValue: String {
        rawValue
    }
}

struct UserProfile: Hashable, Codable {
    let id: String
    let email: String
    let displayName: String
    let roles: [String]
    let hasAssignedRole: Bool?
    let mobileAuthPlanned: Bool

    var normalizedRoles: Set<String> {
        Set(roles.map { $0.lowercased() })
    }

    var isApprovedForSignIn: Bool {
        hasAssignedRole ?? !roles.isEmpty
    }

    var canManageInventory: Bool {
        normalizedRoles.contains("admin")
    }

    var canCreateOrEditELN: Bool {
        normalizedRoles.contains("admin") || normalizedRoles.contains("researcher")
    }

    var canReviewELN: Bool {
        normalizedRoles.contains("admin") || normalizedRoles.contains("reviewer")
    }
}
