import SwiftUI

struct HomeView: View {
    let onSelectInventory: (Int) -> Void
    let onBrowseInventory: () -> Void
    let onBrowseELN: () -> Void

    @EnvironmentObject private var store: AppStore
    @State private var isScannerPresented = false
    @State private var pendingInventoryID: Int?
    @State private var scanErrorMessage: String?
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
                subtitle: "Browse inventory items on mobile.",
                symbol: "shippingbox.fill",
                accent: Color(red: 0.12, green: 0.36, blue: 0.69)
            ) {
                onBrowseInventory()
            }

            homeButton(
                title: "ELN Records",
                subtitle: "Open ELN entries in read-only mode.",
                symbol: "doc.text.fill",
                accent: Color(red: 0.42, green: 0.33, blue: 0.14)
            ) {
                onBrowseELN()
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
                let item = try await store.resolveInventoryPayload(payload)
                pendingInventoryID = item.id
            } catch {
                scanErrorMessage = error.localizedDescription
            }

            isResolvingScan = false
            isScannerPresented = false
        }
    }
}
