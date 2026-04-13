using System.Text.Json;
using ElectronicLabNotebook.Data;
using System.Text;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Controllers;

[Authorize]
public sealed class RecordsController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRecordService _recordService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public RecordsController(IRecordService recordService, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _recordService = recordService;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? q, RecordStatus? status, string? userId, string? sortBy, bool descending = true, CancellationToken cancellationToken = default)
    {
        var effectiveSortBy = string.IsNullOrWhiteSpace(sortBy) ? "UpdatedAtUtc" : sortBy;
        var records = await _recordService.SearchAsync(new RecordSearchRequest
        {
            Query = q ?? string.Empty,
            Status = status,
            UserId = userId ?? string.Empty,
            SortBy = effectiveSortBy,
            Descending = descending
        }, cancellationToken);

        var userOptions = await _context.Users
            .OrderBy(x => x.UserName)
            .ThenBy(x => x.Email)
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName)
                    ? (x.UserName ?? x.Email ?? x.Id)
                    : $"{x.DisplayName} ({x.UserName ?? x.Email ?? x.Id})",
                x.Id))
            .ToListAsync(cancellationToken);

        ViewBag.Query = q ?? string.Empty;
        ViewBag.Status = status;
        ViewBag.UserId = userId ?? string.Empty;
        ViewBag.UserOptions = userOptions;
        ViewBag.SortBy = effectiveSortBy;
        ViewBag.Descending = descending;
        return View(records);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var record = await _recordService.GetAsync(id, cancellationToken);
        return record is null ? NotFound() : View(record);
    }

    public async Task<IActionResult> ExportTrail(int id, CancellationToken cancellationToken)
    {
        var record = await _recordService.GetAsync(id, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        var auditLogs = (await _context.AuditLogs
            .Where(x => x.EntityType == nameof(ExperimentRecord) && x.EntityId == id.ToString())
            .ToListAsync(cancellationToken))
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        var actorIds = auditLogs
            .Select(x => x.ActorUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var actorNames = await _context.Users
            .Where(x => actorIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                Name = !string.IsNullOrWhiteSpace(x.DisplayName) ? x.DisplayName : (x.Email ?? x.UserName ?? x.Id)
            })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var content = BuildTrailText(record, auditLogs, actorNames);
        var fileName = $"record-{SanitizeFileName(record.ExperimentCode)}-trail.txt";
        return File(Encoding.UTF8.GetBytes(content), "text/plain; charset=utf-8", fileName);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    [WindowsWriteAccess]
    public async Task<IActionResult> Create(string? inventoryCode, CancellationToken cancellationToken)
    {
        return View("Edit", await BuildEditorViewModelAsync(null, inventoryCode, cancellationToken));
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    [WindowsWriteAccess]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var record = await _recordService.GetAsync(id, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        return View(await BuildEditorViewModelAsync(record, null, cancellationToken));
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> SuggestExperimentCode(string? projectName, string? client, int? recordId, CancellationToken cancellationToken)
    {
        var code = await _recordService.GetSuggestedExperimentCodeAsync(projectName ?? string.Empty, client ?? string.Empty, recordId, cancellationToken);
        return Json(new { experimentCode = code });
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Save(RecordEditorViewModel model, CancellationToken cancellationToken)
    {
        model.ExperimentCode = await _recordService.GetSuggestedExperimentCodeAsync(model.ProjectName ?? string.Empty, model.Title, model.Id, cancellationToken);
        if (!ModelState.IsValid)
        {
            await PopulateEditorListsAsync(model, cancellationToken);
            return View("Edit", model);
        }

        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var request = ToSaveRequest(model);

        try
        {
            if (model.Id.HasValue)
            {
                var updated = await _recordService.UpdateAsync(model.Id.Value, request, actorUserId, "Web", null, cancellationToken);
                if (updated is null)
                {
                    ModelState.AddModelError(string.Empty, "This record can no longer be edited in its current workflow state.");
                    await PopulateEditorListsAsync(model, cancellationToken);
                    return View("Edit", model);
                }

                return RedirectToAction(nameof(Details), new { id = updated.Id });
            }

            var created = await _recordService.CreateAsync(request, actorUserId, "Web", null, cancellationToken);
            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (DbUpdateException exception) when (IsExperimentCodeConflict(exception))
        {
            ModelState.AddModelError(nameof(model.ExperimentCode), "Experiment code could not be generated uniquely for this project and client. Please retry.");
            await PopulateEditorListsAsync(model, cancellationToken);
            return View("Edit", model);
        }
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var submitted = await _recordService.SubmitAsync(id, actorUserId, "Web", cancellationToken);
        if (!submitted)
        {
            TempData["RecordError"] = "The record could not be submitted for review.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Approve(int id, string? comment, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var approved = await _recordService.ApproveAsync(id, actorUserId, comment ?? string.Empty, "Web", cancellationToken);
        if (!approved)
        {
            TempData["RecordError"] = "The record could not be approved.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Reject(int id, string? comment, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var rejected = await _recordService.RejectAsync(id, actorUserId, comment ?? string.Empty, "Web", cancellationToken);
        if (!rejected)
        {
            TempData["RecordError"] = "The record could not be rejected.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var deleted = await _recordService.DeleteAsync(id, actorUserId, "Web", cancellationToken);
        if (!deleted)
        {
            TempData["RecordError"] = "The record could not be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<RecordEditorViewModel> BuildEditorViewModelAsync(ExperimentRecord? record, string? initialInventoryCode, CancellationToken cancellationToken)
    {
        var model = new RecordEditorViewModel();
        var currentUserName = _userManager.GetUserName(User) ?? User.Identity?.Name ?? string.Empty;
        if (record is not null)
        {
            model.Id = record.Id;
            model.Title = record.Title;
            model.ExperimentCode = record.ExperimentCode;
            model.ConductedOn = record.ConductedOn;
            model.ProjectName = record.ProjectName;
            model.PrincipalInvestigator = string.IsNullOrWhiteSpace(record.PrincipalInvestigator) ? currentUserName : record.PrincipalInvestigator;
            model.TemplateId = record.TemplateId;
            model.RichTextContent = record.RichTextContent;
            model.NotebookBlocksJson = BuildNotebookBlocksJson(record);
            model.FlowchartJson = record.FlowchartJson;
            model.FlowchartPreviewPath = record.FlowchartPreviewPath;
            model.SignatureStatement = string.IsNullOrWhiteSpace(record.SignatureStatement) ? model.SignatureStatement : record.SignatureStatement;
            model.SignatureDate = record.SignatureTimestampUtc.HasValue
                ? DateOnly.FromDateTime(record.SignatureTimestampUtc.Value.UtcDateTime)
                : null;
            model.Status = record.Status;
            model.ReviewComment = record.ReviewComment;
            model.InstrumentLinksJson = JsonSerializer.Serialize(record.InstrumentLinks.Select(x => new RecordInstrumentLinkRequest { InstrumentId = x.InstrumentId, UsageHours = x.UsageHours }), JsonOptions);
        }
        else
        {
            model.NotebookBlocksJson = "[]";
            model.PrincipalInvestigator = currentUserName;
        }

        await PopulateEditorListsAsync(model, cancellationToken);
        model.InitialInventoryCode = initialInventoryCode;
        if (record is null)
        {
            model.ExperimentCode = await _recordService.GetSuggestedExperimentCodeAsync(model.ProjectName ?? string.Empty, model.Title, null, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(initialInventoryCode) && string.IsNullOrWhiteSpace(record?.RichTextContent))
        {
            var option = model.InventoryLookupOptions.FirstOrDefault(x => string.Equals(x.Code, initialInventoryCode, StringComparison.OrdinalIgnoreCase));
            if (option is not null)
            {
                model.RichTextContent = $"""
<div class="inventory-inline-card" contenteditable="false" data-inventory-id="{option.Id}">
    <a href="{option.DetailPath}" target="_blank" rel="noreferrer">{System.Net.WebUtility.HtmlEncode(option.Label)}</a>
    <span class="inventory-pill-hours">Linked</span>
</div>
<p><br></p>
""";
                model.InstrumentLinksJson = JsonSerializer.Serialize(new[]
                {
                    new RecordInstrumentLinkRequest
                    {
                        InstrumentId = option.Id
                    }
                }, JsonOptions);
            }
        }

        return model;
    }

    private async Task PopulateEditorListsAsync(RecordEditorViewModel model, CancellationToken cancellationToken)
    {
        model.TemplateOptions = await _context.RecordTemplates
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync(cancellationToken);

        model.TemplatePayloads = await _context.RecordTemplates
            .OrderBy(x => x.Name)
            .Select(x => new RecordTemplatePayloadViewModel
            {
                Id = x.Id,
                Name = x.Name,
                DefaultRichText = string.IsNullOrWhiteSpace(x.DefaultRichText) ? "<p><br></p>" : x.DefaultRichText
            })
            .ToListAsync(cancellationToken);

        model.InstrumentOptions = await _context.Instruments
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.Code)
            .Select(x => new SelectListItem($"[{x.ItemType}] {x.Code} - {x.Name}", x.Id.ToString()))
            .ToListAsync(cancellationToken);

        model.InventoryLookupOptions = await _context.Instruments
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.Code)
            .Select(x => new RecordInventoryLookupOptionViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Label = $"[{x.ItemType}] {x.Code} - {x.Name}",
                DetailPath = $"/Inventory/Details/{x.Id}"
            })
            .ToListAsync(cancellationToken);

        model.NotebookLookupOptions = await _context.ExperimentRecords
            .OrderByDescending(x => x.ConductedOn)
            .ThenBy(x => x.ExperimentCode)
            .Where(x => !model.Id.HasValue || x.Id != model.Id.Value)
            .Select(x => new RecordNotebookLookupOptionViewModel
            {
                Id = x.Id,
                Code = x.ExperimentCode,
                Title = x.Title,
                Label = $"{x.ExperimentCode} - {x.Title}",
                DetailPath = $"/Records/Details/{x.Id}"
            })
            .ToListAsync(cancellationToken);
    }

    private static RecordSaveRequest ToSaveRequest(RecordEditorViewModel model)
    {
        return new RecordSaveRequest
        {
            Title = model.Title,
            ExperimentCode = model.ExperimentCode,
            ConductedOn = model.ConductedOn,
            ProjectName = model.ProjectName ?? string.Empty,
            PrincipalInvestigator = model.PrincipalInvestigator ?? string.Empty,
            TemplateId = model.TemplateId,
            RichTextContent = model.RichTextContent ?? string.Empty,
            StructuredDataJson = model.NotebookBlocksJson ?? "[]",
            TableJson = "{\"columns\":[],\"rows\":[]}",
            FlowchartJson = model.FlowchartJson ?? "{\"nodes\":[],\"edges\":[]}",
            FlowchartPreviewPath = model.FlowchartPreviewPath ?? string.Empty,
            SignatureStatement = model.SignatureStatement ?? string.Empty,
            SignatureDate = model.SignatureDate,
            InstrumentLinks = string.IsNullOrWhiteSpace(model.InstrumentLinksJson)
                ? new List<RecordInstrumentLinkRequest>()
                : JsonSerializer.Deserialize<List<RecordInstrumentLinkRequest>>(model.InstrumentLinksJson, JsonOptions) ?? new List<RecordInstrumentLinkRequest>()
        };
    }

    private static string BuildNotebookBlocksJson(ExperimentRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.StructuredDataJson) && record.StructuredDataJson.TrimStart().StartsWith('['))
        {
            return record.StructuredDataJson;
        }

        var fallback = new[]
        {
            new
            {
                type = "text",
                text = record.RichTextContent ?? string.Empty
            }
        };

        return JsonSerializer.Serialize(fallback, JsonOptions);
    }

    private static bool IsExperimentCodeConflict(DbUpdateException exception)
    {
        return exception.InnerException?.Message.Contains("IX_ExperimentRecords_ProjectName_Title_ExperimentCode", StringComparison.OrdinalIgnoreCase) == true
            || exception.Message.Contains("IX_ExperimentRecords_ProjectName_Title_ExperimentCode", StringComparison.OrdinalIgnoreCase)
            || exception.InnerException?.Message.Contains("ExperimentRecords.ExperimentCode", StringComparison.OrdinalIgnoreCase) == true
            || exception.Message.Contains("ExperimentRecords.ExperimentCode", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTrailText(ExperimentRecord record, IReadOnlyList<AuditLog> auditLogs, IReadOnlyDictionary<string, string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Jordi ELN Record Trail");
        builder.AppendLine($"Client: {record.Title}");
        builder.AppendLine($"Experiment code: {record.ExperimentCode}");
        builder.AppendLine($"Current status: {record.Status}");
        builder.AppendLine($"Exported at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine();

        if (auditLogs.Count == 0)
        {
            builder.AppendLine("No audit entries were found for this record.");
            return builder.ToString();
        }

        foreach (var log in auditLogs)
        {
            builder.AppendLine($"[{log.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}] {log.ActionType}");
            builder.AppendLine($"Actor: {ResolveActorName(log.ActorUserId, actorNames)}");
            builder.AppendLine($"Source: {(string.IsNullOrWhiteSpace(log.SourceClient) ? "Unknown" : log.SourceClient)}");

            var beforeSummary = SummarizeRecordJson(log.BeforeJson);
            var afterSummary = SummarizeRecordJson(log.AfterJson);

            if (!string.IsNullOrWhiteSpace(beforeSummary))
            {
                builder.AppendLine($"Before: {beforeSummary}");
            }

            if (!string.IsNullOrWhiteSpace(afterSummary))
            {
                builder.AppendLine($"After: {afterSummary}");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string ResolveActorName(string actorUserId, IReadOnlyDictionary<string, string> actorNames)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return "Unknown";
        }

        return actorNames.TryGetValue(actorUserId, out var name) ? name : actorUserId;
    }

    private static string SummarizeRecordJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            AppendJsonSummaryPart(root, "title", "Client", parts);
            AppendJsonSummaryPart(root, "experimentCode", "Code", parts);
            AppendJsonSummaryPart(root, "status", "Status", parts);
            AppendJsonSummaryPart(root, "projectName", "Project", parts);
            AppendJsonSummaryPart(root, "reviewComment", "Review comment", parts);
            AppendJsonSummaryPart(root, "signatureStatement", "E-signature", parts);
            AppendJsonSummaryPart(root, "updatedAtUtc", "Updated", parts);
            return string.Join(" | ", parts);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void AppendJsonSummaryPart(JsonElement root, string propertyName, string label, List<string> parts)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        var value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label}={value}");
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
    }
}
