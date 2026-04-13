using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ElectronicLabNotebook.Controllers;

[Authorize(Roles = Roles.Admin + "," + Roles.Researcher + "," + Roles.Reviewer)]
public sealed class TemplatesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TemplatesController(ApplicationDbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var templates = await _context.RecordTemplates
            .OrderBy(x => x.Name)
            .Select(x => new TemplateSummaryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                PreviewHtml = string.IsNullOrWhiteSpace(x.DefaultRichText) ? "<p><br></p>" : x.DefaultRichText,
                HasHighlights = x.DefaultRichText.Contains("<mark", StringComparison.OrdinalIgnoreCase),
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        return View(templates);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var template = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        return View(ToEditorViewModel(template));
    }

    public async Task<IActionResult> ExportTrail(int id, CancellationToken cancellationToken)
    {
        var template = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        var auditLogs = (await _context.AuditLogs
            .Where(x => x.EntityType == nameof(RecordTemplate) && x.EntityId == id.ToString())
            .ToListAsync(cancellationToken))
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();

        var content = BuildTrailText(template, auditLogs);
        var fileName = $"template-{SanitizeFileName(template.Name)}-trail.txt";
        return File(Encoding.UTF8.GetBytes(content), "text/plain; charset=utf-8", fileName);
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View("Edit", await BuildEditorViewModelAsync(new TemplateEditorViewModel(), cancellationToken));
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var template = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return template is null ? NotFound() : View(await BuildEditorViewModelAsync(ToEditorViewModel(template), cancellationToken));
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TemplateEditorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateEditorLookupsAsync(model, cancellationToken);
            return View("Edit", model);
        }

        if (model.Id.HasValue)
        {
            var existing = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == model.Id.Value, cancellationToken);
            if (existing is null)
            {
                return NotFound();
            }

            if (existing.Status == RecordStatus.Submitted || existing.Status == RecordStatus.Approved)
            {
                TempData["TemplateError"] = "This template can no longer be edited in its current workflow state.";
                return RedirectToAction(nameof(Details), new { id = existing.Id });
            }

            var beforeJson = JsonSerializer.Serialize(existing, JsonOptions);
            Apply(existing, model);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.WriteAsync("TemplateUpdated", nameof(RecordTemplate), existing.Id.ToString(), User.Identity?.Name ?? string.Empty, "Web", beforeJson, JsonSerializer.Serialize(existing, JsonOptions));
        }
        else
        {
            var template = new RecordTemplate();
            Apply(template, model);
            _context.RecordTemplates.Add(template);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.WriteAsync("TemplateCreated", nameof(RecordTemplate), template.Id.ToString(), User.Identity?.Name ?? string.Empty, "Web", string.Empty, JsonSerializer.Serialize(template, JsonOptions));
        }
        TempData["TemplateMessage"] = "Template saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        var template = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            TempData["TemplateError"] = "The selected template could not be found.";
            return RedirectToAction(nameof(Index));
        }

        if (template.Status != RecordStatus.Draft && template.Status != RecordStatus.Rejected)
        {
            TempData["TemplateError"] = "The template could not be submitted for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var beforeJson = JsonSerializer.Serialize(template, JsonOptions);
        template.Status = RecordStatus.Submitted;
        template.SubmittedByUserId = User.Identity?.Name ?? string.Empty;
        template.SubmittedAtUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("TemplateSubmitted", nameof(RecordTemplate), template.Id.ToString(), User.Identity?.Name ?? string.Empty, "Web", beforeJson, JsonSerializer.Serialize(template, JsonOptions));

        TempData["TemplateMessage"] = "Template submitted for review.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    [WindowsWriteAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? comment, CancellationToken cancellationToken)
    {
        return await ReviewAsync(id, RecordStatus.Approved, comment, cancellationToken);
    }

    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    [WindowsWriteAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? comment, CancellationToken cancellationToken)
    {
        return await ReviewAsync(id, RecordStatus.Rejected, comment, cancellationToken);
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var template = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            TempData["TemplateError"] = "The selected template could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var beforeJson = JsonSerializer.Serialize(template, JsonOptions);
        _context.RecordTemplates.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("TemplateDeleted", nameof(RecordTemplate), template.Id.ToString(), User.Identity?.Name ?? string.Empty, "Web", beforeJson, string.Empty);
        TempData["TemplateMessage"] = "Template deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static TemplateEditorViewModel ToEditorViewModel(RecordTemplate template)
    {
        return new TemplateEditorViewModel
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            DefaultRichText = string.IsNullOrWhiteSpace(template.DefaultRichText) ? "<p><br></p>" : template.DefaultRichText,
            Status = template.Status,
            ReviewComment = template.ReviewComment,
            SubmittedAtUtc = template.SubmittedAtUtc,
            ReviewedAtUtc = template.ReviewedAtUtc
        };
    }

    private async Task<TemplateEditorViewModel> BuildEditorViewModelAsync(TemplateEditorViewModel model, CancellationToken cancellationToken = default)
    {
        await PopulateEditorLookupsAsync(model, cancellationToken);
        return model;
    }

    private async Task PopulateEditorLookupsAsync(TemplateEditorViewModel model, CancellationToken cancellationToken)
    {
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

    private static void Apply(RecordTemplate template, TemplateEditorViewModel model)
    {
        template.Name = model.Name.Trim();
        template.Description = model.Description.Trim();
        template.DefaultRichText = string.IsNullOrWhiteSpace(model.DefaultRichText) ? "<p><br></p>" : model.DefaultRichText;
    }

    private async Task<IActionResult> ReviewAsync(int id, RecordStatus targetStatus, string? comment, CancellationToken cancellationToken)
    {
        var template = await _context.RecordTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            TempData["TemplateError"] = "The selected template could not be found.";
            return RedirectToAction(nameof(Index));
        }

        if (template.Status != RecordStatus.Submitted)
        {
            TempData["TemplateError"] = "The template is not waiting for review.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var beforeJson = JsonSerializer.Serialize(template, JsonOptions);
        template.Status = targetStatus;
        template.ReviewedByUserId = User.Identity?.Name ?? string.Empty;
        template.ReviewedAtUtc = DateTimeOffset.UtcNow;
        template.ReviewComment = comment?.Trim() ?? string.Empty;
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync(targetStatus == RecordStatus.Approved ? "TemplateApproved" : "TemplateRejected", nameof(RecordTemplate), template.Id.ToString(), User.Identity?.Name ?? string.Empty, "Web", beforeJson, JsonSerializer.Serialize(template, JsonOptions));

        TempData["TemplateMessage"] = targetStatus == RecordStatus.Approved
            ? "Template approved."
            : "Template rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static string BuildTrailText(RecordTemplate template, IReadOnlyList<AuditLog> auditLogs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Jordi ELN Template Trail");
        builder.AppendLine($"Name: {template.Name}");
        builder.AppendLine($"Current status: {template.Status}");
        builder.AppendLine($"Exported at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine();

        if (auditLogs.Count == 0)
        {
            builder.AppendLine("No audit entries were found for this template.");
            return builder.ToString();
        }

        foreach (var log in auditLogs)
        {
            builder.AppendLine($"[{log.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}] {log.ActionType}");
            builder.AppendLine($"Actor: {(string.IsNullOrWhiteSpace(log.ActorUserId) ? "Unknown" : log.ActorUserId)}");
            builder.AppendLine($"Source: {(string.IsNullOrWhiteSpace(log.SourceClient) ? "Unknown" : log.SourceClient)}");

            var beforeSummary = SummarizeTemplateJson(log.BeforeJson);
            var afterSummary = SummarizeTemplateJson(log.AfterJson);
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

    private static string SummarizeTemplateJson(string json)
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
            AppendJsonSummaryPart(root, "name", "Name", parts);
            AppendJsonSummaryPart(root, "status", "Status", parts);
            AppendJsonSummaryPart(root, "reviewComment", "Review comment", parts);
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
