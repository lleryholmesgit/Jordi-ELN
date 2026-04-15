# Azure Container Apps Deployment

This document describes the compliant Microsoft-centric deployment target for Jordi ELN without changing application workflow or business logic.

## Target Architecture

- Runtime: ASP.NET Core 10 in a self-contained Docker container image
- Image registry: Azure Container Registry (ACR)
- Hosting: Azure Container Apps
- Public URL: `appname.jordilabs.rqmplus.com`
- Persistent database: Azure SQL Database
- Secrets: Azure Container Apps secrets or Azure Key Vault references
- Container security: non-root runtime user
- Public ingress: enabled for web-facing access

## Required Azure Resources

- Azure Resource Group
- Azure Container Registry
- Azure Container Apps Environment
- Azure Container App
- Azure SQL Database
- Azure Key Vault or Container Apps secrets
- DNS CNAME for `appname.jordilabs.rqmplus.com`
- TLS certificate for the custom domain

## Required Production Configuration

Do not commit any production secret values. Configure these through Container Apps secrets or Key Vault.

| Setting | Source | Notes |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | Environment variable | `Production` |
| `ASPNETCORE_URLS` | Docker image default | `http://+:8080` |
| `Database__Provider` | Environment variable | `SqlServer` |
| `ConnectionStrings__DefaultConnection` | Secret | Azure SQL connection string |
| `QrCode__SigningKey` | Secret | HMAC signing key for inventory and storage-location QR codes |
| `DataProtection__KeysPath` | Environment variable | Persistent path if multiple replicas are used |
| `FileStorage__RootPath` | Environment variable | Persistent path for attachments |
| `Email__Password` | Secret | Only if SMTP password auth is used |

## Build and Push Image to ACR

Use a unique immutable tag, normally the Git commit SHA.

```powershell
az acr build `
  --registry <acr-name> `
  --image jordi-eln:<git-sha> `
  .
```

## Create or Update Azure Container App

Use Container Apps secrets or Key Vault references for all secret values.

```powershell
az containerapp create `
  --name jordi-eln `
  --resource-group <resource-group> `
  --environment <container-apps-environment> `
  --image <acr-name>.azurecr.io/jordi-eln:<git-sha> `
  --target-port 8080 `
  --ingress external `
  --registry-server <acr-name>.azurecr.io `
  --secrets `
      sql-connection-string="<azure-sql-connection-string>" `
      qr-code-signing-key="<strong-random-signing-key>" `
  --env-vars `
      ASPNETCORE_ENVIRONMENT=Production `
      Database__Provider=SqlServer `
      ConnectionStrings__DefaultConnection=secretref:sql-connection-string `
      QrCode__SigningKey=secretref:qr-code-signing-key `
      DataProtection__KeysPath=/app/App_Data/DataProtectionKeys `
      FileStorage__RootPath=/app/App_Data/Uploads
```

For updates:

```powershell
az containerapp update `
  --name jordi-eln `
  --resource-group <resource-group> `
  --image <acr-name>.azurecr.io/jordi-eln:<git-sha>
```

## Custom Domain

Configure the Container App custom domain as:

```text
appname.jordilabs.rqmplus.com
```

Create the required DNS record and bind the TLS certificate using the Azure portal or `az containerapp hostname`.

## Azure SQL Notes

- Production must use Azure SQL Database.
- Do not allow direct production database connections from outside Azure.
- Prefer private networking or firewall rules scoped to Azure resources managed by IT.
- Store the SQL connection string as a Container Apps secret or Key Vault secret.

## Persistence Notes

The current app stores uploaded attachments through the configured file-storage path. For production, IT should mount persistent storage or migrate this path to an IT-approved storage backing service. The same applies to ASP.NET Core Data Protection keys when multiple replicas or rolling deployments are used.

## Health Check

The app exposes:

```text
/health
```

Use this endpoint for Container Apps health and availability checks.
