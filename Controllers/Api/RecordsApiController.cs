using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicLabNotebook.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/records")]
public sealed class RecordsApiController : ApiControllerBase
{
    private readonly IRecordService _recordService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RecordsApiController(IRecordService recordService, UserManager<ApplicationUser> userManager)
    {
        _recordService = recordService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] RecordStatus? status, CancellationToken cancellationToken)
    {
        var items = await _recordService.SearchAsync(new RecordSearchRequest { Query = query ?? string.Empty, Status = status }, cancellationToken);
        return Ok(items.Select(MapRecord));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _recordService.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(MapRecord(item));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Create([FromBody] RecordSaveRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _recordService.CreateAsync(request, actorUserId, "Api", null, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, MapRecord(entity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Update(int id, [FromBody] RecordSaveRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _recordService.UpdateAsync(id, request, actorUserId, "Api", null, cancellationToken);
        return entity is null ? BadRequest(new { message = "The record cannot be edited in its current state." }) : Ok(MapRecord(entity));
    }

    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Researcher)]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        return await _recordService.SubmitAsync(id, actorUserId, "Api", cancellationToken)
            ? Ok(new { message = "Record submitted." })
            : BadRequest(new { message = "The record could not be submitted." });
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    public async Task<IActionResult> Approve(int id, [FromBody] RecordReviewRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        return await _recordService.ApproveAsync(id, actorUserId, request.Comment ?? string.Empty, "Api", cancellationToken)
            ? Ok(new { message = "Record approved." })
            : BadRequest(new { message = "The record could not be approved." });
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Reviewer)]
    public async Task<IActionResult> Reject(int id, [FromBody] RecordReviewRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        return await _recordService.RejectAsync(id, actorUserId, request.Comment ?? string.Empty, "Api", cancellationToken)
            ? Ok(new { message = "Record rejected." })
            : BadRequest(new { message = "The record could not be rejected." });
    }

    private static RecordResponse MapRecord(ExperimentRecord record)
    {
        return new RecordResponse
        {
            Id = record.Id,
            Client = record.Title,
            Title = record.Title,
            ExperimentCode = record.ExperimentCode,
            ConductedOn = record.ConductedOn,
            ProjectName = record.ProjectName,
            PrincipalInvestigator = record.PrincipalInvestigator,
            RichTextContent = record.RichTextContent,
            StructuredDataJson = record.StructuredDataJson,
            TableJson = record.TableJson,
            FlowchartJson = record.FlowchartJson,
            Status = record.Status,
            ReviewComment = record.ReviewComment,
            SignatureStatement = record.SignatureStatement,
            SignatureTimestampUtc = record.SignatureTimestampUtc,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            CreatedByUser = record.CreatedByUser is null
                ? null
                : new RecordUserResponse
                {
                    Id = record.CreatedByUser.Id,
                    DisplayName = record.CreatedByUser.DisplayName,
                    Email = record.CreatedByUser.Email ?? string.Empty
                },
            Attachments = record.Attachments
                .Select(attachment => new RecordAttachmentResponse
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    Length = attachment.Length,
                    IsImage = attachment.IsImage,
                    UploadedAtUtc = attachment.UploadedAtUtc,
                    UploadedByUserId = attachment.UploadedByUserId
                })
                .ToList(),
            InstrumentLinks = record.InstrumentLinks
                .Select(link => new RecordInstrumentLinkResponse
                {
                    InstrumentId = link.InstrumentId,
                    LinkedAtUtc = link.LinkedAtUtc,
                    UsageNote = link.UsageNote,
                    UsageHours = link.UsageHours,
                    Instrument = link.Instrument is null
                        ? null
                        : new RecordInstrumentResponse
                        {
                            Id = link.Instrument.Id,
                            ItemType = link.Instrument.ItemType,
                            Code = link.Instrument.Code,
                            Name = link.Instrument.Name,
                            Model = link.Instrument.Model,
                            Manufacturer = link.Instrument.Manufacturer,
                            SerialNumber = link.Instrument.SerialNumber,
                            Location = link.Instrument.StorageLocation?.Name ?? link.Instrument.Location,
                            Status = link.Instrument.Status,
                            OwnerName = link.Instrument.OwnerName,
                            CalibrationInfo = link.Instrument.CalibrationInfo,
                            ProductNumber = link.Instrument.ProductNumber,
                            CatalogNumber = link.Instrument.CatalogNumber,
                            LotNumber = link.Instrument.LotNumber,
                            ExpNumber = link.Instrument.ExpNumber,
                            Quantity = link.Instrument.Quantity,
                            Unit = link.Instrument.Unit,
                            OpenedOn = link.Instrument.OpenedOn,
                            ExpiresOn = link.Instrument.ExpiresOn,
                            Notes = link.Instrument.Notes,
                            QrCodeToken = link.Instrument.QrCodeToken,
                            CreatedAtUtc = link.Instrument.CreatedAtUtc
                        }
                })
                .ToList()
        };
    }

    private sealed class RecordResponse
    {
        public int Id { get; set; }
        public string Client { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ExperimentCode { get; set; } = string.Empty;
        public DateOnly ConductedOn { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string PrincipalInvestigator { get; set; } = string.Empty;
        public string RichTextContent { get; set; } = string.Empty;
        public string StructuredDataJson { get; set; } = "{}";
        public string TableJson { get; set; } = "{\"columns\":[],\"rows\":[]}";
        public string FlowchartJson { get; set; } = "{\"nodes\":[],\"edges\":[]}";
        public RecordStatus Status { get; set; }
        public string ReviewComment { get; set; } = string.Empty;
        public string SignatureStatement { get; set; } = string.Empty;
        public DateTimeOffset? SignatureTimestampUtc { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public RecordUserResponse? CreatedByUser { get; set; }
        public List<RecordAttachmentResponse> Attachments { get; set; } = new();
        public List<RecordInstrumentLinkResponse> InstrumentLinks { get; set; } = new();
    }

    private sealed class RecordUserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    private sealed class RecordAttachmentResponse
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Length { get; set; }
        public bool IsImage { get; set; }
        public DateTimeOffset UploadedAtUtc { get; set; }
        public string UploadedByUserId { get; set; } = string.Empty;
    }

    private sealed class RecordInstrumentLinkResponse
    {
        public int InstrumentId { get; set; }
        public DateTimeOffset LinkedAtUtc { get; set; }
        public string UsageNote { get; set; } = string.Empty;
        public decimal? UsageHours { get; set; }
        public RecordInstrumentResponse? Instrument { get; set; }
    }

    private sealed class RecordInstrumentResponse
    {
        public int Id { get; set; }
        public InventoryItemType ItemType { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public InstrumentStatus Status { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string CalibrationInfo { get; set; } = string.Empty;
        public string ProductNumber { get; set; } = string.Empty;
        public string CatalogNumber { get; set; } = string.Empty;
        public string LotNumber { get; set; } = string.Empty;
        public string ExpNumber { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateOnly? OpenedOn { get; set; }
        public DateOnly? ExpiresOn { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string QrCodeToken { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
    }
}
