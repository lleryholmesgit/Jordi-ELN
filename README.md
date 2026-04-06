# Electronic Lab Notebook

An ASP.NET Core full-stack starter for an internal electronic lab notebook with English-only UI and contracts.

## Included features
- ASP.NET Core 10 MVC + REST API monolith
- ASP.NET Core Identity with `Admin`, `Researcher`, and `Reviewer` roles
- Experiment records with draft, submitted, approved, and rejected workflow
- Mixed-content records with rich text, flowchart JSON, attachments, e-signature, and linked inventory items
- Unified inventory for instruments and chemicals
- Chemical tracking with product number, cat number, lot number, exp number, quantity, unit, opened-on date, and expiry date
- Instrument QR payload generation with a mobile-friendly scan page
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

## Notes
- Database initialization uses `EnsureCreated()` plus a lightweight schema updater for frictionless local evolution; migrate to EF Core migrations before production.
- Browser QR scanning uses the Barcode Detector API when available and falls back to manual payload input.
- The iOS app should call `POST /api/inventory/resolve-qr` with the scanned payload, then link the returned instrument through the record APIs.
