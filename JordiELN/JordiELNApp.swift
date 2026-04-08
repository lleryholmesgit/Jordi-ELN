import SwiftUI

@main
struct JordiELNApp: App {
    @StateObject private var store = AppStore()

    var body: some Scene {
        WindowGroup {
            AppRootView()
                .environmentObject(store)
        }
    }
}
