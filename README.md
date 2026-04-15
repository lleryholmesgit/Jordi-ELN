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
- Docker container support for Azure Container Apps
- Azure SQL Database provider support for production

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
3. Run `.\run-app.ps1` on Windows, or run `dotnet run` with `ASPNETCORE_ENVIRONMENT=Development`.
4. Sign in with one of the seeded accounts.

## Running on the local network
- Use `.\run-lan-server.ps1` to bind Kestrel to `0.0.0.0:5055`.
- The script prints the device IPv4 addresses so Windows and iOS devices on the same network can open `http://<device-ip>:5055`.
- iPhone browser sessions are limited to read and scan workflows. iPad and Windows access is controlled by account role.
- Health check endpoint: `/health`
- Run `.\enable-lan-firewall.ps1` once from an administrator PowerShell window if Windows Firewall blocks inbound access to port `5055`.
- Run `.\enable-lan-access.ps1` from an administrator PowerShell window to switch the current Windows network to `Private` and enable discovery-related firewall rules.

## Docker
The application includes a production Dockerfile for Azure Container Apps.

- Build image:
  ```powershell
  docker build -t jordi-eln:local .
  ```
- Run locally with development settings:
  ```powershell
  docker run --rm -p 8080:8080 `
    -e ASPNETCORE_ENVIRONMENT=Development `
    -e ASPNETCORE_URLS=http://+:8080 `
    jordi-eln:local
  ```
- The runtime container runs as non-root UID/GID `10001`.
- Base images are pinned to explicit Microsoft .NET tags, not `latest`.
- The container listens on port `8080`.

## Azure Container Apps deployment
The IT-approved production target is:

- Image registry: Azure Container Registry
- Hosting: Azure Container Apps
- Public URL pattern: `appname.jordilabs.rqmplus.com`
- Database: Azure SQL Database
- Secrets: Azure Container Apps secrets or Azure Key Vault

Production must set these values outside the repository:

```text
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<secret reference>
QrCode__SigningKey=<secret reference>
DataProtection__KeysPath=/app/App_Data/DataProtectionKeys
FileStorage__RootPath=/app/App_Data/Uploads
```

See `docs/azure-container-apps-deployment.md` for the deployment command outline.

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
- `POST /api/inventory/{id}/check-in`
- `POST /api/inventory/{id}/check-out`
- `GET /api/records`
- `GET /api/records/{id}`
- `POST /api/attachments/upload`

## Deployment and migration readiness
- The app now accepts forwarded headers for future reverse-proxy or cloud hosting.
- Data protection keys default to `App_Data/DataProtectionKeys`; production can override this with `DataProtection__KeysPath`.
- SQLite is for local development only. Production must use Azure SQL Database via `Database__Provider=SqlServer`.
- Production QR signing key must be provided through Azure Container Apps secrets or Azure Key Vault.
- No production credentials, `.env` files, connection strings, or API keys should be committed to the repository.
- Submit production-bound changes through a GitHub pull request and review before merging to `main`.

## Source control and secrets policy
- Source is managed in GitHub.
- Dependencies are pinned in the project file and Dockerfile.
- Do not commit `.env` files or environment-specific secret files.
- Do not commit Azure SQL credentials, SMTP passwords, QR signing keys, or API keys.
- Use Azure Container Apps secrets or Azure Key Vault for production secrets.

## Notes
- Database initialization uses `EnsureCreated()` plus a lightweight schema updater for frictionless local evolution; migrate to EF Core migrations before production.
- Browser QR scanning uses the Barcode Detector API when available and falls back to manual payload input.
- The iOS app should call `POST /api/inventory/resolve-qr` with the scanned payload, then link the returned instrument through the record APIs.
