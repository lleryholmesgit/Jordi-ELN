using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/attachments")]
public sealed class AttachmentsApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IRecordService _recordService;
    private readonly IFileStorageService _fileStorageService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AttachmentsApiController(ApplicationDbContext context, IRecordService recordService, IFileStorageService fileStorageService, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _recordService = recordService;
        _fileStorageService = fileStorageService;
        _userManager = userManager;
    }

    [HttpPost("record/{recordId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Upload(int recordId, List<IFormFile> files, CancellationToken cancellationToken)
    {
        var record = await _context.ExperimentRecords.FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        var actorUserId = GetActorUserId(_userManager);
        var saveRequest = new RecordSaveRequest
        {
            Title = record.Title,
            ExperimentCode = record.ExperimentCode,
            ConductedOn = record.ConductedOn,
            ProjectName = record.ProjectName,
            PrincipalInvestigator = record.PrincipalInvestigator,
            TemplateId = record.TemplateId,
            RichTextContent = record.RichTextContent,
            StructuredDataJson = record.StructuredDataJson,
            TableJson = record.TableJson,
            FlowchartJson = record.FlowchartJson,
            FlowchartPreviewPath = record.FlowchartPreviewPath,
            SignatureStatement = record.SignatureStatement,
            InstrumentLinks = await _context.RecordInstrumentLinks
                .Where(x => x.ExperimentRecordId == recordId)
                .Select(x => new RecordInstrumentLinkRequest { InstrumentId = x.InstrumentId, UsageHours = x.UsageHours })
                .ToListAsync(cancellationToken)
        };

        var updated = await _recordService.UpdateAsync(recordId, saveRequest, actorUserId, "Api", files, cancellationToken);
        return updated is null ? BadRequest(new { message = "Attachments cannot be added in the current record state." }) : Ok(updated.Attachments);
    }

    [HttpGet("{attachmentId:int}")]
    public async Task<IActionResult> Download(int attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _context.ExperimentAttachments.FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return NotFound();
        }

        var file = await _fileStorageService.GetAsync(attachment, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("{attachmentId:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Delete(int attachmentId, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var deleted = await _recordService.RemoveAttachmentAsync(attachmentId, actorUserId, "Api", cancellationToken);
        return deleted is null ? NotFound() : Ok(new { message = "Attachment deleted." });
    }
}
