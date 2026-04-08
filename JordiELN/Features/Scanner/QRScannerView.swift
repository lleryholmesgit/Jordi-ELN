import AVFoundation
import SwiftUI
import UIKit

struct QRScannerView: View {
    let onCancel: () -> Void
    let onScanned: (String) -> Void

    @State private var cameraErrorMessage: String?
    @State private var isManualEntryPresented = false
    @State private var manualCode = ""

    var body: some View {
        NavigationStack {
            ZStack(alignment: .bottom) {
                scannerLayer
                    .ignoresSafeArea()

                VStack(spacing: 14) {
                    RoundedRectangle(cornerRadius: 24, style: .continuous)
                        .stroke(.white.opacity(0.95), lineWidth: 3)
                        .frame(width: 240, height: 240)
                        .overlay(alignment: .top) {
                            Text("Align QR inside frame")
                                .font(.headline)
                                .padding(.horizontal, 12)
                                .padding(.vertical, 8)
                                .background(.ultraThinMaterial, in: Capsule())
                                .offset(y: -24)
                        }

                    VStack(alignment: .leading, spacing: 10) {
                        Text("Scan inventory QR")
                            .font(.headline)
                        Text("Supported formats include raw IDs like `INV-24001`, custom `inventory:INV-24001`, or detail URLs that include the inventory ID.")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)

                        Button {
                            isManualEntryPresented = true
                        } label: {
                            Label("Manual entry / simulator mode", systemImage: "keyboard")
                                .font(.headline)
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.borderedProminent)
                        .tint(Color(red: 0.04, green: 0.43, blue: 0.32))
                    }
                    .padding(18)
                    .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 26, style: .continuous))
                    .padding(.horizontal, 20)
                    .padding(.bottom, 24)
                }
            }
            .background(Color.black)
            .navigationTitle("QR Scan")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Close") {
                        onCancel()
                    }
                }
            }
            .sheet(isPresented: $isManualEntryPresented) {
                manualEntrySheet
                    .presentationDetents([.medium])
                    .presentationDragIndicator(.visible)
            }
            .alert("Camera unavailable", isPresented: cameraAlertPresented) {
                Button("Use manual entry") {
                    isManualEntryPresented = true
                }
                Button("Close", role: .cancel) {
                    onCancel()
                }
            } message: {
                Text(cameraErrorMessage ?? "")
            }
        }
    }

    @ViewBuilder
    private var scannerLayer: some View {
        #if targetEnvironment(simulator)
        LinearGradient(
            colors: [Color.black, Color(red: 0.06, green: 0.10, blue: 0.09)],
            startPoint: .top,
            endPoint: .bottom
        )
        .overlay {
            VStack(spacing: 12) {
                Image(systemName: "iphone.gen3.radiowaves.left.and.right")
                    .font(.system(size: 48))
                    .foregroundStyle(.white.opacity(0.8))
                Text("Camera preview is not available in Simulator")
                    .font(.headline)
                    .foregroundStyle(.white)
                Text("Use manual entry below to test the scan-to-detail flow.")
                    .font(.subheadline)
                    .foregroundStyle(.white.opacity(0.75))
            }
            .padding()
        }
        #else
        CameraScannerRepresentable(
            onScanned: onScanned,
            onFailure: { message in
                cameraErrorMessage = message
            }
        )
        #endif
    }

    private var manualEntrySheet: some View {
        NavigationStack {
            Form {
                Section("Paste or type a QR payload") {
                    TextField("INV-24001", text: $manualCode)
                        .textInputAutocapitalization(.characters)
                        .autocorrectionDisabled()

                    Button("Use sample inventory code") {
                        manualCode = "inventory:INV-24001"
                    }
                }

                Section("Examples") {
                    Text("INV-24002")
                    Text("inventory:INV-24003")
                    Text("https://jordi.local/inventory/INV-24001")
                }
            }
            .navigationTitle("Manual Scan")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") {
                        isManualEntryPresented = false
                    }
                }

                ToolbarItem(placement: .topBarTrailing) {
                    Button("Open") {
                        let payload = manualCode.trimmingCharacters(in: .whitespacesAndNewlines)
                        guard !payload.isEmpty else {
                            return
                        }

                        isManualEntryPresented = false
                        onScanned(payload)
                    }
                    .disabled(manualCode.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }

    private var cameraAlertPresented: Binding<Bool> {
        Binding(
            get: { cameraErrorMessage != nil },
            set: { newValue in
                if !newValue {
                    cameraErrorMessage = nil
                }
            }
        )
    }
}

private struct CameraScannerRepresentable: UIViewControllerRepresentable {
    let onScanned: (String) -> Void
    let onFailure: (String) -> Void

    func makeUIViewController(context: Context) -> ScannerViewController {
        ScannerViewController(onScanned: onScanned, onFailure: onFailure)
    }

    func updateUIViewController(_ uiViewController: ScannerViewController, context: Context) {
    }

    static func dismantleUIViewController(_ uiViewController: ScannerViewController, coordinator: ()) {
        uiViewController.stopSession()
    }
}

@MainActor
private final class ScannerViewController: UIViewController, @preconcurrency AVCaptureMetadataOutputObjectsDelegate {
    private let session = AVCaptureSession()
    private var previewLayer: AVCaptureVideoPreviewLayer?
    private var hasConfiguredSession = false
    private var hasDeliveredCode = false

    private let onScanned: (String) -> Void
    private let onFailure: (String) -> Void

    init(onScanned: @escaping (String) -> Void, onFailure: @escaping (String) -> Void) {
        self.onScanned = onScanned
        self.onFailure = onFailure
        super.init(nibName: nil, bundle: nil)
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = .black
        configureIfNeeded()
    }

    override func viewDidLayoutSubviews() {
        super.viewDidLayoutSubviews()
        previewLayer?.frame = view.bounds
    }

    override func viewWillAppear(_ animated: Bool) {
        super.viewWillAppear(animated)
        if hasConfiguredSession, !session.isRunning {
            session.startRunning()
        }
    }

    override func viewWillDisappear(_ animated: Bool) {
        super.viewWillDisappear(animated)
        stopSession()
    }

    func stopSession() {
        if session.isRunning {
            session.stopRunning()
        }
    }

    private func configureIfNeeded() {
        guard !hasConfiguredSession else {
            return
        }

        hasConfiguredSession = true

        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            setUpSession()
        case .notDetermined:
            AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
                DispatchQueue.main.async {
                    if granted {
                        self?.setUpSession()
                    } else {
                        self?.onFailure("Camera access is required for live QR scanning on iPhone.")
                    }
                }
            }
        default:
            onFailure("Camera access is not available. You can still use manual entry for QR payloads.")
        }
    }

    private func setUpSession() {
        guard let captureDevice = AVCaptureDevice.default(for: .video) else {
            onFailure("No camera device was found on this iPhone.")
            return
        }

        do {
            let input = try AVCaptureDeviceInput(device: captureDevice)
            if session.canAddInput(input) {
                session.addInput(input)
            } else {
                onFailure("The camera input could not be attached to the scan session.")
                return
            }

            let output = AVCaptureMetadataOutput()
            if session.canAddOutput(output) {
                session.addOutput(output)
                output.setMetadataObjectsDelegate(self, queue: .main)
                output.metadataObjectTypes = [.qr]
            } else {
                onFailure("The QR metadata output could not be attached to the scan session.")
                return
            }

            let previewLayer = AVCaptureVideoPreviewLayer(session: session)
            previewLayer.videoGravity = .resizeAspectFill
            self.previewLayer?.removeFromSuperlayer()
            self.previewLayer = previewLayer
            view.layer.insertSublayer(previewLayer, at: 0)
            previewLayer.frame = view.bounds

            session.startRunning()
        } catch {
            onFailure("The camera session failed to start: \(error.localizedDescription)")
        }
    }

    func metadataOutput(
        _ output: AVCaptureMetadataOutput,
        didOutput metadataObjects: [AVMetadataObject],
        from connection: AVCaptureConnection
    ) {
        guard !hasDeliveredCode,
              let object = metadataObjects.first as? AVMetadataMachineReadableCodeObject,
              let value = object.stringValue else {
            return
        }

        hasDeliveredCode = true
        stopSession()
        onScanned(value)
    }
}
