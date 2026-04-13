import SwiftUI

enum RootTab: Hashable {
    case home
    case inventory
    case eln
}

struct AppRootView: View {
    @EnvironmentObject private var store: AppStore
    @State private var selectedTab: RootTab = .home
    @State private var homePath: [AppRoute] = []
    @State private var inventoryPath: [AppRoute] = []
    @State private var elnPath: [AppRoute] = []

    var body: some View {
        Group {
            if store.isAuthenticated {
                TabView(selection: $selectedTab) {
                    NavigationStack(path: $homePath) {
                        HomeView(
                            onSelectInventory: { inventoryID in
                                homePath.append(.inventory(inventoryID))
                            },
                            onSelectELN: { elnID in
                                homePath.append(.eln(elnID))
                            },
                            onBrowseInventory: {
                                selectedTab = .inventory
                            },
                            onBrowseELN: {
                                selectedTab = .eln
                            }
                        )
                        .navigationDestination(for: AppRoute.self) { route in
                            RouteDestinationView(
                                route: route,
                                onOpenInventory: { homePath.append(.inventory($0)) },
                                onOpenELN: { homePath.append(.eln($0)) }
                            )
                        }
                    }
                    .tabItem {
                        Label("Home", systemImage: "house.fill")
                    }
                    .tag(RootTab.home)

                    NavigationStack(path: $inventoryPath) {
                        InventoryListView { inventoryID in
                            inventoryPath.append(.inventory(inventoryID))
                        }
                        .navigationDestination(for: AppRoute.self) { route in
                            RouteDestinationView(
                                route: route,
                                onOpenInventory: { inventoryPath.append(.inventory($0)) },
                                onOpenELN: { inventoryPath.append(.eln($0)) }
                            )
                        }
                    }
                    .tabItem {
                        Label("Inventory", systemImage: "shippingbox.fill")
                    }
                    .tag(RootTab.inventory)

                    NavigationStack(path: $elnPath) {
                        ELNListView { elnID in
                            elnPath.append(.eln(elnID))
                        }
                        .navigationDestination(for: AppRoute.self) { route in
                            RouteDestinationView(
                                route: route,
                                onOpenInventory: { elnPath.append(.inventory($0)) },
                                onOpenELN: { elnPath.append(.eln($0)) }
                            )
                        }
                    }
                    .tabItem {
                        Label("ELN", systemImage: "doc.text.fill")
                    }
                    .tag(RootTab.eln)
                }
                .tint(Color(red: 0.04, green: 0.43, blue: 0.32))
            } else {
                LoginView()
            }
        }
        .sheet(isPresented: $store.isConnectionSettingsPresented) {
            ConnectionSettingsView()
                .environmentObject(store)
        }
        .onChange(of: store.isAuthenticated) { _, isAuthenticated in
            guard !isAuthenticated else {
                return
            }

            selectedTab = .home
            homePath = []
            inventoryPath = []
            elnPath = []
        }
        .task {
            await store.restoreProfileIfPossible()
        }
    }
}
