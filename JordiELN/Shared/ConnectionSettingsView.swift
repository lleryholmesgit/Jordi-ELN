import SwiftUI

struct ConnectionSettingsView: View {
    @Environment(\.dismiss) private var dismiss
    @EnvironmentObject private var store: AppStore

    @State private var baseURL = ""
    @State private var email = ""
    @State private var password = ""
    @State private var isConnecting = false
    @State private var errorMessage: String?

    var body: some View {
        NavigationStack {
            Form {
                Section("Server") {
                    TextField("http://127.0.0.1:5050", text: $baseURL)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.URL)
                        .autocorrectionDisabled()

                    Text("Use `127.0.0.1` for Simulator when the ASP.NET app runs on the same Mac. Use your Mac's LAN IP for a real iPhone.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)

                    Text("Do not enter `0.0.0.0` in the app. That is only the server bind address.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }

                Section("App sign-in") {
                    TextField("researcher@lab.local", text: $email)
                        .textInputAutocapitalization(.never)
                        .keyboardType(.emailAddress)
                        .autocorrectionDisabled()

                    SecureField("Password", text: $password)

                    Text("The same session is used for both Inventory and ELN browsing.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }

                if store.isAuthenticated {
                    Section {
                        Button("Sign Out", role: .destructive) {
                            Task {
                                await store.signOut()
                                dismiss()
                            }
                        }
                    }
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle("Connection")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Close") {
                        dismiss()
                    }
                }

                ToolbarItem(placement: .topBarTrailing) {
                    Button(isConnecting ? "Connecting..." : "Save") {
                        connect()
                    }
                    .disabled(isConnecting || baseURL.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
            .onAppear {
                baseURL = store.serverBaseURL
                email = store.savedEmail
            }
        }
    }

    private func connect() {
        let trimmedBaseURL = baseURL.trimmingCharacters(in: .whitespacesAndNewlines)
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)
        let trimmedPassword = password.trimmingCharacters(in: .whitespacesAndNewlines)
        let connectionChanged = trimmedBaseURL != store.serverBaseURL || trimmedEmail != store.savedEmail

        if trimmedBaseURL.contains("0.0.0.0") {
            errorMessage = "Do not use `0.0.0.0` in the app. Use `http://127.0.0.1:5050` in Simulator, or your Mac's LAN IP on a real iPhone."
            return
        }

        if store.isAuthenticated && !connectionChanged && trimmedPassword.isEmpty {
            dismiss()
            return
        }

        if !trimmedEmail.isEmpty || connectionChanged || !store.isAuthenticated {
            guard !trimmedEmail.isEmpty, !trimmedPassword.isEmpty else {
                errorMessage = APIClientError.missingCredentials.localizedDescription
                return
            }
        }

        isConnecting = true
        errorMessage = nil

        Task {
            defer {
                isConnecting = false
            }

            do {
                if !trimmedEmail.isEmpty {
                    try await store.configureAndSignIn(
                        baseURL: trimmedBaseURL,
                        email: trimmedEmail,
                        password: trimmedPassword
                    )
                } else if connectionChanged {
                    store.persistConnectionSettings(baseURL: trimmedBaseURL, email: trimmedEmail)
                }
                dismiss()
            } catch {
                errorMessage = error.localizedDescription
            }
        }
    }
}
