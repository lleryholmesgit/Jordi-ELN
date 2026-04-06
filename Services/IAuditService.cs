namespace ElectronicLabNotebook.Services;

public interface IAuditService
{
    Task WriteAsync(string actionType, string entityType, string entityId, string actorUserId, string sourceClient, string beforeJson, string afterJson);
}