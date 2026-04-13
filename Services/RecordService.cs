using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Services;

public sealed class RecordService : IRecordService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { ReferenceHandler = ReferenceHandler.IgnoreCycles };
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IAuditService _auditService;

    public RecordService(ApplicationDbContext context, IFileStorageService fileStorageService, IAuditService auditService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<ExperimentRecord>> SearchAsync(RecordSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.ExperimentRecords
            .Include(x => x.Template)
            .Include(x => x.Attachments)
            .Include(x => x.CreatedByUser)
            .Include(x => x.InstrumentLinks)
                .ThenInclude(x => x.Instrument)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            query = query.Where(x =>
                x.Title.Contains(request.Query) ||
                x.ExperimentCode.Contains(request.Query) ||
                x.ProjectName.Contains(request.Query));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(x => x.CreatedByUserId == request.UserId);
        }

        var items = await query.ToListAsync(cancellationToken);
        return ApplySort(items, request).ToList();
    }

    public Task<ExperimentRecord?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.ExperimentRecords
            .Include(x => x.Template)
            .Include(x => x.Attachments)
            .Include(x => x.ReviewHistory)
            .Include(x => x.InstrumentLinks)
                .ThenInclude(x => x.Instrument)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<string> GetSuggestedExperimentCodeAsync(string projectName, string client, int? recordId = null, CancellationToken cancellationToken = default)
    {
        var normalizedProject = (projectName ?? string.Empty).Trim();
        var normalizedClient = (client ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedProject) || string.IsNullOrWhiteSpace(normalizedClient))
        {
            return string.Empty;
        }

        if (recordId.HasValue)
        {
            var current = await _context.ExperimentRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == recordId.Value, cancellationToken);
            if (current is not null
                && string.Equals(current.ProjectName, normalizedProject, StringComparison.OrdinalIgnoreCase)
                && string.Equals(current.Title, normalizedClient, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(current.ExperimentCode, out var currentCode)
                && currentCode > 0)
            {
                return currentCode.ToString();
            }
        }

        var existingCodes = await _context.ExperimentRecords
            .Where(x => x.ProjectName == normalizedProject && x.Title == normalizedClient && (!recordId.HasValue || x.Id != recordId.Value))
            .Select(x => x.ExperimentCode)
            .ToListAsync(cancellationToken);

        var maxSequence = existingCodes
            .Select(code => int.TryParse(code, out var parsed) ? parsed : 0)
            .DefaultIfEmpty(0)
            .Max();

        return (maxSequence + 1).ToString();
    }

    public async Task<ExperimentRecord> CreateAsync(RecordSaveRequest request, string actorUserId, string sourceClient, IEnumerable<IFormFile>? files = null, CancellationToken cancellationToken = default)
    {
        var generatedExperimentCode = await GetSuggestedExperimentCodeAsync(request.ProjectName ?? string.Empty, request.Title, null, cancellationToken);
        var entity = new ExperimentRecord
        {
            Title = request.Title,
            ExperimentCode = generatedExperimentCode,
            ConductedOn = request.ConductedOn,
            ProjectName = request.ProjectName ?? string.Empty,
            PrincipalInvestigator = request.PrincipalInvestigator ?? string.Empty,
            TemplateId = request.TemplateId,
            RichTextContent = request.RichTextContent ?? string.Empty,
            StructuredDataJson = request.StructuredDataJson ?? "{}",
            TableJson = request.TableJson ?? "{\"columns\":[],\"rows\":[]}",
            FlowchartJson = request.FlowchartJson ?? "{\"nodes\":[],\"edges\":[]}",
            FlowchartPreviewPath = request.FlowchartPreviewPath ?? string.Empty,
            SignatureStatement = request.SignatureStatement ?? string.Empty,
            SignatureTimestampUtc = request.SignatureDate.HasValue
                ? new DateTimeOffset(request.SignatureDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : null,
            SignatureHash = ComputeSignatureHash(request),
            CreatedByUserId = actorUserId,
            LastUpdatedByUserId = actorUserId
        };

        ApplyInstrumentLinks(entity, request.InstrumentLinks, actorUserId);

        _context.ExperimentRecords.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await SaveFilesAsync(entity, files, actorUserId, cancellationToken);

        await _auditService.WriteAsync("RecordCreated", nameof(ExperimentRecord), entity.Id.ToString(), actorUserId, sourceClient, string.Empty, JsonSerializer.Serialize(entity, JsonOptions));
        return entity;
    }

    public async Task<ExperimentRecord?> UpdateAsync(int id, RecordSaveRequest request, string actorUserId, string sourceClient, IEnumerable<IFormFile>? files = null, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRecords
            .Include(x => x.InstrumentLinks)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        if (entity.Status == RecordStatus.Submitted || entity.Status == RecordStatus.Approved)
        {
            return null;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);
        var generatedExperimentCode = await GetSuggestedExperimentCodeAsync(request.ProjectName ?? string.Empty, request.Title, id, cancellationToken);

        entity.Title = request.Title;
        entity.ExperimentCode = generatedExperimentCode;
        entity.ConductedOn = request.ConductedOn;
        entity.ProjectName = request.ProjectName ?? string.Empty;
        entity.PrincipalInvestigator = request.PrincipalInvestigator ?? string.Empty;
        entity.TemplateId = request.TemplateId;
        entity.RichTextContent = request.RichTextContent ?? string.Empty;
        entity.StructuredDataJson = request.StructuredDataJson ?? "{}";
        entity.TableJson = request.TableJson ?? "{\"columns\":[],\"rows\":[]}";
        entity.FlowchartJson = request.FlowchartJson ?? "{\"nodes\":[],\"edges\":[]}";
        entity.FlowchartPreviewPath = request.FlowchartPreviewPath ?? string.Empty;
        entity.SignatureStatement = request.SignatureStatement ?? string.Empty;
        entity.SignatureTimestampUtc = request.SignatureDate.HasValue
            ? new DateTimeOffset(request.SignatureDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : null;
        entity.SignatureHash = ComputeSignatureHash(request);
        entity.LastUpdatedByUserId = actorUserId;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _context.RecordInstrumentLinks.RemoveRange(entity.InstrumentLinks.ToList());
        entity.InstrumentLinks.Clear();
        ApplyInstrumentLinks(entity, request.InstrumentLinks, actorUserId);

        await SaveFilesAsync(entity, files, actorUserId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync("RecordUpdated", nameof(ExperimentRecord), entity.Id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity, JsonOptions));
        return entity;
    }

    public async Task<bool> SubmitAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRecords.Include(x => x.ReviewHistory).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null || (entity.Status != RecordStatus.Draft && entity.Status != RecordStatus.Rejected))
        {
            return false;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);
        entity.Status = RecordStatus.Submitted;
        entity.SubmittedByUserId = actorUserId;
        entity.SubmittedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        entity.ReviewHistory.Add(new ReviewAction
        {
            ActorUserId = actorUserId,
            Action = "Submitted"
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("RecordSubmitted", nameof(ExperimentRecord), id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity, JsonOptions));
        return true;
    }

    public Task<bool> ApproveAsync(int id, string actorUserId, string comment, string sourceClient, CancellationToken cancellationToken = default)
    {
        return ReviewAsync(id, actorUserId, comment, RecordStatus.Approved, "Approved", sourceClient, cancellationToken);
    }

    public Task<bool> RejectAsync(int id, string actorUserId, string comment, string sourceClient, CancellationToken cancellationToken = default)
    {
        return ReviewAsync(id, actorUserId, comment, RecordStatus.Rejected, "Rejected", sourceClient, cancellationToken);
    }

    public async Task<ExperimentAttachment?> RemoveAttachmentAsync(int attachmentId, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var attachment = await _context.ExperimentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        _context.ExperimentAttachments.Remove(attachment);
        await _context.SaveChangesAsync(cancellationToken);
        await _fileStorageService.DeleteAsync(attachment, cancellationToken);
        await _auditService.WriteAsync("AttachmentRemoved", nameof(ExperimentAttachment), attachment.Id.ToString(), actorUserId, sourceClient, JsonSerializer.Serialize(attachment, JsonOptions), string.Empty);
        return attachment;
    }

    public async Task<bool> DeleteAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ExperimentRecords
            .Include(x => x.Attachments)
            .Include(x => x.InstrumentLinks)
            .Include(x => x.ReviewHistory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);

        foreach (var attachment in entity.Attachments.ToList())
        {
            await _fileStorageService.DeleteAsync(attachment, cancellationToken);
        }

        _context.ExperimentAttachments.RemoveRange(entity.Attachments);
        _context.RecordInstrumentLinks.RemoveRange(entity.InstrumentLinks);
        _context.ReviewActions.RemoveRange(entity.ReviewHistory);
        _context.ExperimentRecords.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync("RecordDeleted", nameof(ExperimentRecord), id.ToString(), actorUserId, sourceClient, beforeJson, string.Empty);
        return true;
    }

    private async Task<bool> ReviewAsync(int id, string actorUserId, string comment, RecordStatus targetStatus, string action, string sourceClient, CancellationToken cancellationToken)
    {
        var entity = await _context.ExperimentRecords.Include(x => x.ReviewHistory).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null || entity.Status != RecordStatus.Submitted)
        {
            return false;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);
        entity.Status = targetStatus;
        entity.ReviewedByUserId = actorUserId;
        entity.ReviewedAtUtc = DateTimeOffset.UtcNow;
        entity.ReviewComment = comment ?? string.Empty;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        entity.ReviewHistory.Add(new ReviewAction
        {
            ActorUserId = actorUserId,
            Action = action,
            Comment = comment ?? string.Empty
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync($"Record{action}", nameof(ExperimentRecord), id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity, JsonOptions));
        return true;
    }

    private void ApplyInstrumentLinks(ExperimentRecord entity, IEnumerable<RecordInstrumentLinkRequest> requestedLinks, string actorUserId)
    {
        foreach (var link in requestedLinks.Where(x => x.InstrumentId > 0).DistinctBy(x => x.InstrumentId))
        {
            entity.InstrumentLinks.Add(new RecordInstrumentLink
            {
                InstrumentId = link.InstrumentId,
                LinkedByUserId = actorUserId,
                UsageNote = string.Empty,
                UsageHours = link.UsageHours
            });
        }
    }

    private async Task SaveFilesAsync(ExperimentRecord entity, IEnumerable<IFormFile>? files, string actorUserId, CancellationToken cancellationToken)
    {
        if (files is null)
        {
            return;
        }

        foreach (var file in files.Where(x => x.Length > 0))
        {
            var attachment = await _fileStorageService.SaveAsync(entity.Id, file, actorUserId, cancellationToken);
            entity.Attachments.Add(attachment);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string ComputeSignatureHash(RecordSaveRequest request)
    {
        var raw = $"{request.Title}|{request.ProjectName}|{request.ConductedOn}|{request.RichTextContent}|{request.FlowchartJson}|{request.SignatureStatement}|{request.SignatureDate}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private static IEnumerable<ExperimentRecord> ApplySort(IEnumerable<ExperimentRecord> items, RecordSearchRequest request)
    {
        var sortBy = request.SortBy?.Trim() ?? string.Empty;
        var descending = request.Descending;

        return sortBy.ToLowerInvariant() switch
        {
            "experimentcode" or "code" => descending
                ? items.OrderByDescending(x => x.ExperimentCode, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(x => x.ExperimentCode, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc),
            "title" => descending
                ? items.OrderByDescending(x => x.Title, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc),
            "conductedon" or "date" => descending
                ? items.OrderByDescending(x => x.ConductedOn).ThenByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(x => x.ConductedOn).ThenByDescending(x => x.UpdatedAtUtc),
            "projectname" or "project" => descending
                ? items.OrderByDescending(x => x.ProjectName, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(x => x.ProjectName, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc),
            "status" => descending
                ? items.OrderByDescending(x => x.Status).ThenByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(x => x.Status).ThenByDescending(x => x.UpdatedAtUtc),
            "username" or "createdby" => descending
                ? items.OrderByDescending(GetRecordUserName, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(GetRecordUserName, StringComparer.OrdinalIgnoreCase).ThenByDescending(x => x.UpdatedAtUtc),
            _ => descending
                ? items.OrderByDescending(x => x.UpdatedAtUtc)
                : items.OrderBy(x => x.UpdatedAtUtc)
        };
    }

    private static string GetRecordUserName(ExperimentRecord record)
    {
        return record.CreatedByUser?.UserName
            ?? record.CreatedByUser?.Email
            ?? record.CreatedByUser?.DisplayName
            ?? record.CreatedByUserId;
    }
}
