import SwiftUI

struct ELNListView: View {
    let onSelectELN: (Int) -> Void

    @EnvironmentObject private var store: AppStore
    @State private var searchText = ""

    private var filteredEntries: [ELNEntry] {
        guard !searchText.isEmpty else {
            return store.elnEntries
        }

        let query = searchText.localizedLowercase
        return store.elnEntries.filter { entry in
            entry.experimentCode.localizedLowercase.contains(query) ||
            entry.title.localizedLowercase.contains(query) ||
            entry.authorName.localizedLowercase.contains(query) ||
            entry.projectName.localizedLowercase.contains(query)
        }
    }

    var body: some View {
        List {
            if let errorMessage = store.elnErrorMessage, store.elnEntries.isEmpty {
                Section {
                    ContentUnavailableView(
                        "ELN unavailable",
                        systemImage: "doc.badge.ellipsis",
                        description: Text(errorMessage)
                    )

                    Button("Configure connection") {
                        store.isConnectionSettingsPresented = true
                    }
                }
            } else if store.isELNLoading && store.elnEntries.isEmpty {
                Section {
                    HStack {
                        Spacer()
                        ProgressView("Loading ELN records...")
                        Spacer()
                    }
                }
            } else {
                Section {
                    if store.isTabletClient {
                        VStack(alignment: .leading, spacing: 8) {
                            Label("ELN is backed by `/api/records`. iPad can use role-based workspace access alongside the native browser.", systemImage: "rectangle.on.rectangle")
                                .font(.subheadline)
                                .foregroundStyle(.secondary)

                            if let workspaceURL = store.fullWorkspaceURL(path: "/Records") {
                                Link("Open full ELN workspace", destination: workspaceURL)
                                    .font(.subheadline.weight(.semibold))
                            }
                        }
                    } else {
                        Label("ELN is backed by `/api/records` and stays streamlined for iPhone viewing.", systemImage: "lock.doc.fill")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }
                }

                Section("ELN entries") {
                    ForEach(filteredEntries) { entry in
                        Button {
                            onSelectELN(entry.id)
                        } label: {
                            VStack(alignment: .leading, spacing: 6) {
                                Text(entry.title)
                                    .font(.headline)
                                    .foregroundStyle(.primary)
                                Text("\(entry.experimentCode) • \(entry.authorName)")
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                                Text(entry.summaryText)
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                                    .lineLimit(2)
                            }
                            .padding(.vertical, 4)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
        }
        .navigationTitle("ELN")
        .listStyle(.insetGrouped)
        .searchable(text: $searchText, prompt: "Search ELN")
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button {
                    store.isConnectionSettingsPresented = true
                } label: {
                    Image(systemName: "server.rack")
                }

                Button {
                    Task {
                        await store.loadELN()
                    }
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .disabled(store.isELNLoading)
            }
        }
        .task {
            if store.elnEntries.isEmpty {
                await store.loadELN()
            }
        }
        .refreshable {
            await store.loadELN()
        }
    }
}
