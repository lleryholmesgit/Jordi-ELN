# IT Compliance Notes

## Scope

These updates make Jordi ELN compatible with the requested Microsoft-centric deployment model without changing the ELN workflow, inventory workflow, QR workflow, account workflow, or user-facing business logic.

## Requirement Mapping

| Requirement | Implementation / Repository Support |
| --- | --- |
| Self-contained Docker container | `Dockerfile` added with multi-stage .NET build and runtime image |
| ACR image storage | Documented ACR build/push flow in `docs/azure-container-apps-deployment.md` |
| Azure Container Apps hosting | Documented ACA deployment and environment variables |
| Dedicated public URL | Custom domain instructions for `appname.jordilabs.rqmplus.com` |
| GitHub source control | Repository remains GitHub-based |
| README setup/deployment/usage | README updated with Docker, Azure SQL, ACA, and security notes |
| No committed credentials/secrets | Production settings are secret placeholders only |
| Pull-request workflow | Documented in README |
| .NET acceptable for complexity | Existing ASP.NET Core 10 app retained |
| Pinned dependencies | NuGet package versions and Docker base tags are pinned |
| Azure SQL Database | `Database__Provider=SqlServer` supported for production |
| No hardcoded DB credentials | Connection string must be injected via Container Apps secret or Key Vault |
| Container secrets/Key Vault | Documented secret mapping |
| Non-root container | Runtime container uses UID/GID `10001` |
| No `latest` image tags | Dockerfile uses explicit .NET SDK/runtime tags |
| Public web-facing app | Container Apps ingress documented as external |

## Production Secrets

Do not commit these values:

- Azure SQL connection string
- QR signing key
- SMTP credentials
- Any future API keys

Use Azure Container Apps secrets or Azure Key Vault references.

## Database Provider

Local development may continue to use SQLite. Production must set:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<secret reference>
```

## Operational Notes

- The app already supports forwarded headers for proxy/container hosting.
- The app already exposes `/health`.
- Azure SQL migrations should be managed through the IT-approved release process before production rollout.
- If more than one Container Apps replica is enabled, persist ASP.NET Core Data Protection keys outside ephemeral container storage.
