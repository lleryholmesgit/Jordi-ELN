# Jordi ELN

An ASP.NET Core full-stack starter for an internal electronic lab notebook with English-only UI and contracts.

## Included features
- ASP.NET Core 10 MVC + REST API monolith
- ASP.NET Core Identity with `Admin`, `Researcher`, and `Reviewer` roles
- Self-service account registration with admin-assigned roles only
- Experiment records with draft, submitted, approved, and rejected workflow
- Mixed-content records with rich text, flowchart JSON, attachments, e-signature, and linked inventory items
- Unified inventory for instruments and chemicals
- Chemical tracking with product number, cat number, lot number, exp number, quantity, unit, opened-on date, and expiry date
- Instrument QR payload generation with a mobile-friendly scan page
- Shared lab notebook templates with highlighted review/change sections
- Shared REST APIs for future iOS integration
- Audit trail and e-signature placeholder fields

## Seeded accounts
- `admin@lab.local` / `LabNotebook1`
- `researcher@lab.local` / `LabNotebook1`
- `reviewer@lab.local` / `LabNotebook1`

## Project structure
- `Program.cs`: host setup and endpoint mapping
- `Data/`: EF Core context and seed data
- `Models/`: domain entities and workflow enums
- `Services/`: record, inventory, file storage, QR, and audit services
- `Controllers/`: MVC UI controllers and API controllers
- `Views/`: English-only Razor views
- `wwwroot/js/`: flowchart editor and QR scanning scripts

## Running locally
1. Install the .NET 10 SDK or retarget the project to a locally available stable SDK.
2. From the project root, run `dotnet restore`.
3. Run `dotnet run`.
4. Sign in with one of the seeded accounts.

## Running on the local network
- Use `.\run-lan-server.ps1` to bind Kestrel to `0.0.0.0:5055`.
- The script prints the device IPv4 addresses so Windows and iOS devices on the same network can open `http://<device-ip>:5055`.
- iOS browser sessions are intentionally read-only. Windows write access is still controlled by account role.
- Health check endpoint: `/health`
- Run `.\enable-lan-firewall.ps1` once from an administrator PowerShell window if Windows Firewall blocks inbound access to port `5055`.
- Run `.\enable-lan-access.ps1` from an administrator PowerShell window to switch the current Windows network to `Private` and enable discovery-related firewall rules.

## Registration and access control
- New users can register from the sign-in page.
- Registration does not grant any role automatically.
- Only the existing `Admin` account can assign `Admin`, `Researcher`, or `Reviewer` from the Admin page.
- Users without an assigned role cannot sign in until an admin approves access.

## iOS-ready API surface currently in the repo
- `POST /api/auth/login`
- `POST /api/auth/register`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `GET /api/inventory`
- `GET /api/inventory/{id}`
- `POST /api/inventory/resolve-qr`
- `GET /api/records`
- `GET /api/records/{id}`
- `POST /api/attachments/upload`

## Deployment and migration readiness
- The app now accepts forwarded headers for future reverse-proxy or cloud hosting.
- Data protection keys are stored under `App_Data/DataProtectionKeys`; move this to a shared durable location before multi-instance deployment.
- SQLite is fine for a single-device or small LAN deployment. Move to SQL Server or PostgreSQL before larger on-prem or cloud deployment.
- Replace the development QR signing key in `appsettings.json` before production use.

## Notes
- Database initialization uses `EnsureCreated()` plus a lightweight schema updater for frictionless local evolution; migrate to EF Core migrations before production.
- Browser QR scanning uses the Barcode Detector API when available and falls back to manual payload input.
- The iOS app should call `POST /api/inventory/resolve-qr` with the scanned payload, then link the returned instrument through the record APIs.
