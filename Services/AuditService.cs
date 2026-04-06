using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.Services;

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task WriteAsync(string actionType, string entityType, string entityId, string actorUserId, string sourceClient, string beforeJson, string afterJson)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            SourceClient = sourceClient,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        });

        await _context.SaveChangesAsync();
    }
}