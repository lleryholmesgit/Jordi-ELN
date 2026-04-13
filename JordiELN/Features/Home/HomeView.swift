import SwiftUI

struct HomeView: View {
    let onSelectInventory: (Int) -> Void
    let onSelectELN: (Int) -> Void
    let onBrowseInventory: () -> Void
    let onBrowseELN: () -> Void

    @EnvironmentObject private var store: AppStore
    @State private var isScannerPresented = false
    @State private var pendingInventoryID: Int?
    @State private var scanErrorMessage: String?
    @State private var scanResultPresentation: ScanResultPresentation?
    @State private var isResolvingScan = false

    var body: some View {
        VStack(spacing: 18) {
            Spacer()

            homeButton(
                title: "QR Scan",
                subtitle: "Scan a code and jump straight to inventory detail.",
                symbol: "qrcode.viewfinder",
                accent: Color(red: 0.04, green: 0.43, blue: 0.32)
            ) {
                isScannerPresented = true
            }

            homeButton(
                title: "Inventory",
                subtitle: store.isTabletClient ? "Browse inventory and jump into full workspace tools on iPad." : "Browse inventory items on mobile.",
                symbol: "shippingbox.fill",
                accent: Color(red: 0.12, green: 0.36, blue: 0.69)
            ) {
                onBrowseInventory()
            }

            homeButton(
                title: "ELN Records",
                subtitle: store.isTabletClient ? "Browse ELN and use full role-based workspace access on iPad." : "Open ELN entries in read-only mode.",
                symbol: "doc.text.fill",
                accent: Color(red: 0.42, green: 0.33, blue: 0.14)
            ) {
                onBrowseELN()
            }

            if store.canUseFullWorkspace, let workspaceURL = store.fullWorkspaceURL() {
                Link(destination: workspaceURL) {
                    Label("Open Full Workspace", systemImage: "safari")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, 16)
                        .background(Color(.secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 22, style: .continuous))
                }
                .buttonStyle(.plain)
            }

            Spacer()
        }
        .padding(20)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(Color(.systemGroupedBackground))
        .navigationTitle("Jordi ELN")
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button {
                    store.isConnectionSettingsPresented = true
                } label: {
                    Image(systemName: "server.rack")
                }

                Button {
                    Task {
                        await store.signOut()
                    }
                } label: {
                    Image(systemName: "rectangle.portrait.and.arrow.right")
                }
            }
        }
        .sheet(isPresented: $isScannerPresented) {
            QRScannerView(
                onCancel: {
                    isScannerPresented = false
                },
                onScanned: { payload in
                    handleScan(payload)
                }
            )
        }
        .sheet(item: $scanResultPresentation) { presentation in
            ScanResultView(
                resolution: presentation.resolution,
                onDismiss: {
                    scanResultPresentation = nil
                },
                onOpenInventory: { inventoryID in
                    scanResultPresentation = nil
                    pendingInventoryID = inventoryID
                },
                onOpenELN: { elnID in
                    scanResultPresentation = nil
                    onSelectELN(elnID)
                }
            )
            .environmentObject(store)
        }
        .alert("QR code not recognized", isPresented: scanErrorAlertIsPresented) {
            Button("OK", role: .cancel) {
                scanErrorMessage = nil
            }
        } message: {
            Text(scanErrorMessage ?? "")
        }
        .overlay {
            if isResolvingScan {
                ProgressView("Resolving QR code...")
                    .padding(20)
                    .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 18, style: .continuous))
            }
        }
        .onChange(of: isScannerPresented) { _, newValue in
            guard !newValue, let inventoryID = pendingInventoryID else {
                return
            }

            pendingInventoryID = nil
            onSelectInventory(inventoryID)
        }
    }

    private func homeButton(
        title: String,
        subtitle: String,
        symbol: String,
        accent: Color,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack(spacing: 16) {
                Image(systemName: symbol)
                    .font(.system(size: 24, weight: .semibold))
                    .foregroundStyle(.white)
                    .frame(width: 56, height: 56)
                    .background(accent, in: RoundedRectangle(cornerRadius: 18, style: .continuous))

                VStack(alignment: .leading, spacing: 4) {
                    Text(title)
                        .font(.title3.bold())
                        .foregroundStyle(.primary)
                    Text(subtitle)
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                Image(systemName: "chevron.right")
                    .font(.headline.weight(.semibold))
                    .foregroundStyle(.tertiary)
            }
            .padding(20)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(Color(.secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 26, style: .continuous))
        }
        .buttonStyle(.plain)
    }

    private var scanErrorAlertIsPresented: Binding<Bool> {
        Binding(
            get: { scanErrorMessage != nil },
            set: { newValue in
                if !newValue {
                    scanErrorMessage = nil
                }
            }
        )
    }

    private func handleScan(_ payload: String) {
        isResolvingScan = true

        Task {
            do {
                let resolution = try await store.resolveScanPayload(payload)
                scanResultPresentation = ScanResultPresentation(resolution: resolution)
            } catch {
                scanErrorMessage = error.localizedDescription
            }

            isResolvingScan = false
            if scanResultPresentation != nil {
                isScannerPresented = false
            }
        }
    }
}
