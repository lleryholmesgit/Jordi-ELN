import SwiftUI
import WebKit

struct ELNDetailScreen: View {
    let elnID: Int
    let onOpenInventory: (Int) -> Void
    let onOpenELN: (Int) -> Void

    @EnvironmentObject private var store: AppStore
    @State private var entry: ELNEntry?
    @State private var errorMessage: String?
    @State private var isLoading = false

    var body: some View {
        Group {
            if let entry {
                ELNDetailView(
                    entry: entry,
                    linkedInventory: store.linkedInventory(forELNID: elnID),
                    onOpenInventory: onOpenInventory,
                    onOpenELN: onOpenELN
                )
            } else if isLoading {
                ProgressView("Loading ELN detail...")
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
                    .background(Color(.systemGroupedBackground))
            } else {
                ContentUnavailableView(
                    "ELN entry not found",
                    systemImage: "doc.badge.magnifyingglass",
                    description: Text(errorMessage ?? "The ELN detail could not be loaded.")
                )
            }
        }
        .task(id: elnID) {
            await load()
        }
    }

    private func load() async {
        if let cached = store.elnEntry(id: elnID) {
            entry = cached
            return
        }

        isLoading = true
        defer {
            isLoading = false
        }

        do {
            entry = try await store.refreshELNEntry(id: elnID)
        } catch {
            errorMessage = error.localizedDescription
        }
    }
}

struct ELNDetailView: View {
    let entry: ELNEntry
    let linkedInventory: [InventoryItem]
    let onOpenInventory: (Int) -> Void
    let onOpenELN: (Int) -> Void

    var body: some View {
        List {
            summarySection

            if !entry.richTextContent.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Section("Notebook") {
                    NotebookRichContentView(
                        htmlFragment: entry.richTextContent,
                        onOpenInventory: onOpenInventory,
                        onOpenELN: onOpenELN
                    )
                }
            }

            if !entry.reviewComment.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Section("Review comment") {
                    Text(entry.reviewComment)
                }
            }

            if !linkedInventory.isEmpty {
                Section("Linked inventory") {
                    ForEach(linkedInventory) { item in
                        Button {
                            onOpenInventory(item.id)
                        } label: {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(item.name)
                                    .font(.headline)
                                    .foregroundStyle(.primary)
                                Text("\(item.code) • \(item.location)")
                                    .font(.subheadline)
                                    .foregroundStyle(.secondary)
                            }
                            .padding(.vertical, 4)
                        }
                        .buttonStyle(.plain)
                    }
                }
            }

            if !entry.attachments.isEmpty {
                Section("Attachments") {
                    ForEach(entry.attachments) { attachment in
                        VStack(alignment: .leading, spacing: 4) {
                            Text(attachment.fileName)
                                .font(.headline)
                            Text("\(attachment.contentType) • \(ByteCountFormatter.string(fromByteCount: attachment.length, countStyle: .file))")
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        }
                        .padding(.vertical, 2)
                    }
                }
            }
        }
        .navigationTitle("")
        .navigationBarTitleDisplayMode(.inline)
        .listStyle(.insetGrouped)
    }

    private var summarySection: some View {
        Section {
            VStack(alignment: .leading, spacing: 12) {
                Text(entry.title)
                    .font(.title3.bold())

                metadataRow(label: "Experiment Code", value: entry.experimentCode, monospaced: true)
                metadataRow(label: "Date", value: formattedDate(entry.conductedOn))
                metadataRow(label: "Project Name", value: entry.projectName)
                metadataRow(label: "Technician", value: entry.authorName)
            }
            .padding(.vertical, 6)
        }
    }

    @ViewBuilder
    private func metadataRow(label: String, value: String, monospaced: Bool = false) -> some View {
        if !value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            VStack(alignment: .leading, spacing: 4) {
                Text(label)
                    .font(.caption)
                    .foregroundStyle(.secondary)

                Text(value)
                    .font(monospaced ? .subheadline.monospaced() : .subheadline)
                    .foregroundStyle(.primary)
            }
        }
    }

    private func formattedDate(_ date: Date?) -> String {
        guard let date else {
            return ""
        }

        return date.formatted(date: .abbreviated, time: .omitted)
    }
}

private struct NotebookRichContentView: View {
    let htmlFragment: String
    let onOpenInventory: (Int) -> Void
    let onOpenELN: (Int) -> Void

    @State private var contentHeight: CGFloat = 200

    var body: some View {
        HTMLContentWebView(
            html: wrappedHTML,
            contentHeight: $contentHeight,
            onOpenInventory: onOpenInventory,
            onOpenELN: onOpenELN
        )
        .frame(minHeight: max(contentHeight, 200), maxHeight: max(contentHeight, 200))
        .listRowInsets(EdgeInsets(top: 8, leading: 0, bottom: 8, trailing: 0))
    }

    private var wrappedHTML: String {
        """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <style>
            :root {
              color-scheme: light;
              --ink: #1d2a24;
              --muted: #647067;
              --line: #d7e2db;
              --panel: #f4f8f5;
              --accent: #0a6d51;
            }
            body {
              margin: 0;
              color: var(--ink);
              font: 16px/1.55 -apple-system, BlinkMacSystemFont, "SF Pro Text", sans-serif;
              background: transparent;
              overflow-wrap: anywhere;
            }
            p, ul, ol, blockquote, figure, table, div.notebook-inline-table, .notebook-equation-block {
              margin: 0 0 14px 0;
            }
            img {
              max-width: 100%;
              height: auto;
              border-radius: 12px;
            }
            a {
              color: var(--accent);
              text-decoration: none;
            }
            table {
              width: 100%;
              border-collapse: collapse;
              font-size: 14px;
              background: white;
            }
            .notebook-inline-table {
              overflow-x: auto;
              border: 1px solid var(--line);
              border-radius: 14px;
              background: white;
            }
            th, td {
              border: 1px solid var(--line);
              padding: 8px 10px;
              vertical-align: top;
              text-align: left;
            }
            th {
              background: #eef5f0;
            }
            .notebook-equation-block, [data-equation-source] {
              display: block;
              border: 1px solid var(--line);
              border-radius: 14px;
              background: var(--panel);
              padding: 12px 14px;
            }
            .equation-badge {
              display: inline-block;
              margin-bottom: 8px;
              padding: 3px 8px;
              border-radius: 999px;
              background: rgba(10,109,81,0.1);
              color: var(--accent);
              font-size: 12px;
              font-weight: 600;
              letter-spacing: 0.02em;
              text-transform: uppercase;
            }
            .equation-render {
              font-family: "Times New Roman", Georgia, serif;
              font-size: 18px;
              line-height: 1.5;
            }
            [data-inventory-id], [data-record-link-id] {
              display: block;
              border: 1px solid var(--line);
              border-radius: 14px;
              background: white;
              padding: 12px 14px;
            }
            .inline-widget-remove, .note-block-handle {
              display: none !important;
            }
          </style>
        </head>
        <body>
          \(sanitizedHTMLFragment)
          <script>
            const ensureEquationBlocks = () => {
              document.querySelectorAll("[data-equation-source]").forEach((widget) => {
                const source = widget.getAttribute("data-equation-source") || widget.textContent || "";
                if (!widget.querySelector(".equation-badge")) {
                  const badge = document.createElement("span");
                  badge.className = "equation-badge";
                  badge.textContent = "Equation";
                  widget.prepend(badge);
                }
                let render = widget.querySelector(".equation-render");
                if (!render) {
                  render = document.createElement("div");
                  render.className = "equation-render";
                  widget.appendChild(render);
                }
                render.textContent = source;
              });
            };
            ensureEquationBlocks();
            const postInternalLink = (type, id) => {
              window.webkit.messageHandlers.internalLink.postMessage({ type, id });
            };
            document.addEventListener("click", (event) => {
              const inventoryCard = event.target.closest("[data-inventory-id]");
              if (inventoryCard) {
                event.preventDefault();
                const id = Number(inventoryCard.getAttribute("data-inventory-id"));
                if (!Number.isNaN(id) && id > 0) {
                  postInternalLink("inventory", id);
                }
                return;
              }

              const recordCard = event.target.closest("[data-record-link-id]");
              if (recordCard) {
                event.preventDefault();
                const id = Number(recordCard.getAttribute("data-record-link-id"));
                if (!Number.isNaN(id) && id > 0) {
                  postInternalLink("eln", id);
                }
                return;
              }

              const anchor = event.target.closest("a[href]");
              if (!anchor) {
                return;
              }

              const href = anchor.getAttribute("href") || "";
              const inventoryMatch = href.match(/\\/Inventory\\/Details\\/(\\d+)/i);
              if (inventoryMatch) {
                event.preventDefault();
                postInternalLink("inventory", Number(inventoryMatch[1]));
                return;
              }

              const recordMatch = href.match(/\\/Records\\/Details\\/(\\d+)/i);
              if (recordMatch) {
                event.preventDefault();
                postInternalLink("eln", Number(recordMatch[1]));
              }
            });
            const sendHeight = () => {
              const height = Math.max(
                document.body.scrollHeight,
                document.documentElement.scrollHeight
              );
              window.webkit.messageHandlers.contentHeight.postMessage(height);
            };
            window.addEventListener("load", sendHeight);
            window.addEventListener("resize", sendHeight);
            setTimeout(sendHeight, 50);
            setTimeout(sendHeight, 250);
          </script>
        </body>
        </html>
        """
    }

    private var sanitizedHTMLFragment: String {
        var value = htmlFragment
            .replacingOccurrences(of: "\u{FEFF}", with: "")
            .replacingOccurrences(of: "\0", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)

        value = value.replacingOccurrences(
            of: #"(?is)\A.*?<body[^>]*>"#,
            with: "",
            options: .regularExpression
        )
        value = value.replacingOccurrences(
            of: #"(?is)</body>.*\z"#,
            with: "",
            options: .regularExpression
        )
        value = value.replacingOccurrences(
            of: #"(?is)<head.*?</head>"#,
            with: "",
            options: .regularExpression
        )
        value = value.replacingOccurrences(
            of: #"(?is)<title.*?</title>"#,
            with: "",
            options: .regularExpression
        )

        value = stripLeadingGarbage(from: value)
        return value
    }

    private func stripLeadingGarbage(from value: String) -> String {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let firstTagIndex = trimmed.firstIndex(of: "<") else {
            return trimmed
        }

        let prefix = trimmed[..<firstTagIndex]
        let cleanedPrefix = prefix.filter { character in
            character.isLetter || character.isNumber
        }

        if cleanedPrefix.isEmpty {
            return String(trimmed[firstTagIndex...])
        }

        return trimmed
    }
}

private struct HTMLContentWebView: UIViewRepresentable {
    let html: String
    @Binding var contentHeight: CGFloat
    let onOpenInventory: (Int) -> Void
    let onOpenELN: (Int) -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(
            contentHeight: $contentHeight,
            onOpenInventory: onOpenInventory,
            onOpenELN: onOpenELN
        )
    }

    func makeUIView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        config.defaultWebpagePreferences.allowsContentJavaScript = true
        config.userContentController.add(context.coordinator, name: "contentHeight")
        config.userContentController.add(context.coordinator, name: "internalLink")

        let webView = WKWebView(frame: .zero, configuration: config)
        webView.isOpaque = false
        webView.backgroundColor = .clear
        webView.scrollView.isScrollEnabled = false
        webView.scrollView.backgroundColor = .clear
        webView.navigationDelegate = context.coordinator
        return webView
    }

    func updateUIView(_ webView: WKWebView, context: Context) {
        if context.coordinator.lastHTML != html {
            context.coordinator.lastHTML = html
            webView.loadHTMLString(html, baseURL: nil)
        }
    }

    static func dismantleUIView(_ webView: WKWebView, coordinator: Coordinator) {
        webView.configuration.userContentController.removeScriptMessageHandler(forName: "contentHeight")
        webView.configuration.userContentController.removeScriptMessageHandler(forName: "internalLink")
    }

    final class Coordinator: NSObject, WKNavigationDelegate, WKScriptMessageHandler {
        @Binding var contentHeight: CGFloat
        let onOpenInventory: (Int) -> Void
        let onOpenELN: (Int) -> Void
        var lastHTML = ""

        init(
            contentHeight: Binding<CGFloat>,
            onOpenInventory: @escaping (Int) -> Void,
            onOpenELN: @escaping (Int) -> Void
        ) {
            _contentHeight = contentHeight
            self.onOpenInventory = onOpenInventory
            self.onOpenELN = onOpenELN
        }

        func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
            if message.name == "contentHeight" {
                if let height = message.body as? CGFloat {
                    contentHeight = height
                } else if let height = message.body as? Double {
                    contentHeight = height
                } else if let height = message.body as? Int {
                    contentHeight = CGFloat(height)
                }
                return
            }

            guard message.name == "internalLink",
                  let body = message.body as? [String: Any],
                  let type = body["type"] as? String,
                  let id = body["id"] as? Int else {
                return
            }

            switch type {
            case "inventory":
                onOpenInventory(id)
            case "eln":
                onOpenELN(id)
            default:
                break
            }
        }

        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            webView.evaluateJavaScript("document.body.scrollHeight") { [weak self] result, _ in
                if let value = result as? Double {
                    self?.contentHeight = value
                } else if let value = result as? Int {
                    self?.contentHeight = CGFloat(value)
                }
            }
        }

        func webView(
            _ webView: WKWebView,
            decidePolicyFor navigationAction: WKNavigationAction,
            decisionHandler: @escaping @MainActor @Sendable (WKNavigationActionPolicy) -> Void
        ) {
            if navigationAction.navigationType == .linkActivated,
               let url = navigationAction.request.url {
                UIApplication.shared.open(url)
                decisionHandler(.cancel)
                return
            }

            decisionHandler(.allow)
        }
    }
}
