import SwiftUI
import UIKit

struct LoginView: View {
    @EnvironmentObject private var store: AppStore

    @State private var baseURL = ""
    @State private var email = ""
    @State private var password = ""
    @State private var errorMessage: String?
    @State private var isSigningIn = false

    var body: some View {
        NavigationStack {
            VStack(spacing: 24) {
                Spacer()

                VStack(spacing: 14) {
                    HStack(spacing: 12) {
                        loginFeatureIcon(systemName: "qrcode.viewfinder")
                        loginFeatureIcon(systemName: "shippingbox.fill")
                        loginFeatureIcon(systemName: "doc.text.fill")
                    }

                    Text("Jordi ELN")
                        .font(.largeTitle.bold())

                    Text("Sign in first, then browse Inventory, ELN records, and QR scan on mobile.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                        .multilineTextAlignment(.center)
                }

                VStack(spacing: 18) {
                    loginField(
                        title: "Server",
                        prompt: "http://127.0.0.1:5050",
                        text: $baseURL,
                        keyboardType: .URL
                    )

                    Text(serverHint)
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                        .frame(maxWidth: .infinity, alignment: .leading)

                    loginField(
                        title: "Email",
                        prompt: "researcher@lab.local",
                        text: $email,
                        keyboardType: .emailAddress
                    )

                    VStack(alignment: .leading, spacing: 8) {
                        Text("Password")
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(.secondary)

                        SecureField("Password", text: $password)
                            .textContentType(.password)
                            .padding(.horizontal, 16)
                            .padding(.vertical, 14)
                            .background(Color(.secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
                    }

                    if let errorMessage {
                        Text(errorMessage)
                            .font(.footnote)
                            .foregroundStyle(.red)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }

                    Button(isSigningIn ? "Signing In..." : "Sign In") {
                        signIn()
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .tint(Color(red: 0.04, green: 0.43, blue: 0.32))
                    .frame(maxWidth: .infinity)
                    .disabled(isSigningIn || !formIsReady)
                }
                .padding(24)
                .background(Color(.systemBackground), in: RoundedRectangle(cornerRadius: 30, style: .continuous))

                VStack(spacing: 8) {
                    Text(primaryAddressExample)
                    Text("Seed login: `researcher@lab.local` / `LabNotebook1`")
                }
                .font(.footnote)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)

                Spacer()
            }
            .padding(24)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(
                LinearGradient(
                    colors: [
                        Color(red: 0.94, green: 0.97, blue: 0.95),
                        Color(red: 0.88, green: 0.93, blue: 0.90)
                    ],
                    startPoint: .top,
                    endPoint: .bottom
                )
            )
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        store.isConnectionSettingsPresented = true
                    } label: {
                        Image(systemName: "server.rack")
                    }
                }
            }
            .onAppear {
                if store.serverBaseURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    #if targetEnvironment(simulator)
                    baseURL = "http://127.0.0.1:5050"
                    #else
                    baseURL = ""
                    #endif
                } else {
                    baseURL = store.serverBaseURL
                }
                email = store.savedEmail
            }
        }
    }

    @ViewBuilder
    private func loginField(
        title: String,
        prompt: String,
        text: Binding<String>,
        keyboardType: UIKeyboardType
    ) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(.secondary)

            TextField(prompt, text: text)
                .textInputAutocapitalization(.never)
                .keyboardType(keyboardType)
                .autocorrectionDisabled()
                .padding(.horizontal, 16)
                .padding(.vertical, 14)
                .background(Color(.secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 18, style: .continuous))
        }
    }

    private func loginFeatureIcon(systemName: String) -> some View {
        RoundedRectangle(cornerRadius: 24, style: .continuous)
            .fill(Color(red: 0.04, green: 0.43, blue: 0.32))
            .frame(width: 64, height: 64)
            .overlay {
                Image(systemName: systemName)
                    .font(.system(size: 24, weight: .semibold))
                    .foregroundStyle(.white)
            }
    }

    private var formIsReady: Bool {
        !baseURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        !email.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        !password.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private func signIn() {
        let trimmedBaseURL = baseURL.trimmingCharacters(in: .whitespacesAndNewlines)

        if trimmedBaseURL.contains("0.0.0.0") {
            errorMessage = "Do not use `0.0.0.0` in the app. Use `http://127.0.0.1:5050` in Simulator, or your Mac's LAN IP on a real iPhone."
            return
        }

        errorMessage = nil
        isSigningIn = true

        Task {
            defer {
                isSigningIn = false
            }

            do {
                try await store.configureAndSignIn(
                    baseURL: baseURL,
                    email: email,
                    password: password
                )
            } catch {
                errorMessage = error.localizedDescription
            }
        }
    }

    private var primaryAddressExample: String {
        #if targetEnvironment(simulator)
        return "Simulator address: `http://127.0.0.1:5050`"
        #else
        return "Real iPhone address: use your Mac's LAN IP like `http://192.168.x.x:5050`"
        #endif
    }

    private var serverHint: String {
        #if targetEnvironment(simulator)
        return "You are on Simulator. Use `127.0.0.1`, not `0.0.0.0`."
        #else
        return "If this app is running on a real iPhone, do not use `127.0.0.1` or `localhost`. Use your Mac's LAN IP."
        #endif
    }
}
