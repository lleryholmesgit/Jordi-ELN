using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicLabNotebook.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryApiController : ApiControllerBase
{
    private readonly IInstrumentService _instrumentService;
    private readonly IQrCodeService _qrCodeService;
    private readonly UserManager<ApplicationUser> _userManager;

    public InventoryApiController(IInstrumentService instrumentService, IQrCodeService qrCodeService, UserManager<ApplicationUser> userManager)
    {
        _instrumentService = instrumentService;
        _qrCodeService = qrCodeService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, [FromQuery] InstrumentStatus? status, [FromQuery] InventoryItemType? itemType, CancellationToken cancellationToken)
    {
        return Ok(await _instrumentService.SearchAsync(new InstrumentSearchRequest { Query = query ?? string.Empty, Status = status, ItemType = itemType }, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var item = await _instrumentService.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create([FromBody] InstrumentSaveRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _instrumentService.CreateAsync(request, actorUserId, "Api", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, [FromBody] InstrumentSaveRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _instrumentService.UpdateAsync(id, request, actorUserId, "Api", cancellationToken);
        return entity is null ? NotFound() : Ok(entity);
    }

    [HttpPost("{id:int}/refresh-qr")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RefreshQr(int id, CancellationToken cancellationToken)
    {
        var actorUserId = GetActorUserId(_userManager);
        var entity = await _instrumentService.RefreshQrAsync(id, actorUserId, "Api", cancellationToken);
        return entity is null ? NotFound() : Ok(new { entity.Id, entity.QrCodeToken, QrCodeSvg = _qrCodeService.GenerateSvg(entity.QrCodeToken) });
    }

    [AllowAnonymous]
    [HttpPost("resolve-qr")]
    public async Task<IActionResult> ResolveQr([FromBody] ResolveQrRequest request, CancellationToken cancellationToken)
    {
        var instrument = await _instrumentService.GetByTokenAsync(request.QrPayload, cancellationToken);
        if (instrument is null)
        {
            return NotFound(new { success = false, message = "The QR code could not be resolved." });
        }

        return Ok(new
        {
            success = true,
            message = "Inventory item resolved.",
            type = "inventory-item",
            instrument = new
            {
                instrument.Id,
                instrument.Code,
                instrument.Name,
                instrument.Model,
                instrument.Location,
                storageLocationId = (int?)null,
                instrument.Status,
                actions = new
                {
                    viewPath = Url.Action("Details", "Inventory", new { id = instrument.Id }),
                    scanOptionsPath = Url.Action("Scan", "Inventory"),
                    addToElnPath = Url.Action("Create", "Records")
                }
            }
        });
    }

    [HttpPost("{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id, CancellationToken cancellationToken)
    {
        var instrument = await _instrumentService.GetAsync(id, cancellationToken);
        if (instrument is null)
        {
            return NotFound(new { message = "Inventory item not found." });
        }

        var actorUserId = GetActorUserId(_userManager);
        var updated = await _instrumentService.UpdateAsync(
            id,
            ToSaveRequest(instrument, InstrumentStatus.Active),
            actorUserId,
            ResolveSourceClient(),
            cancellationToken
        );

        return updated is null
            ? NotFound(new { message = "Inventory item not found." })
            : Ok(new { message = "Inventory item checked in." });
    }

    [HttpPost("{id:int}/check-out")]
    public async Task<IActionResult> CheckOut(int id, CancellationToken cancellationToken)
    {
        var instrument = await _instrumentService.GetAsync(id, cancellationToken);
        if (instrument is null)
        {
            return NotFound(new { message = "Inventory item not found." });
        }

        var actorUserId = GetActorUserId(_userManager);
        var updated = await _instrumentService.UpdateAsync(
            id,
            ToSaveRequest(instrument, InstrumentStatus.Maintenance),
            actorUserId,
            ResolveSourceClient(),
            cancellationToken
        );

        return updated is null
            ? NotFound(new { message = "Inventory item not found." })
            : Ok(new { message = "Inventory item checked out." });
    }

    private string ResolveSourceClient()
    {
        return ClientDeviceDetector.IsAppleMobileClient(Request) ? "iOS" : "Api";
    }

    private static InstrumentSaveRequest ToSaveRequest(Instrument instrument, InstrumentStatus status)
    {
        return new InstrumentSaveRequest
        {
            ItemType = instrument.ItemType,
            Code = instrument.Code,
            Name = instrument.Name,
            Model = instrument.Model,
            Manufacturer = instrument.Manufacturer,
            SerialNumber = instrument.SerialNumber,
            Location = instrument.Location,
            Status = status,
            OwnerName = instrument.OwnerName,
            CalibrationInfo = instrument.CalibrationInfo,
            ProductNumber = instrument.ProductNumber,
            CatalogNumber = instrument.CatalogNumber,
            LotNumber = instrument.LotNumber,
            ExpNumber = instrument.ExpNumber,
            Quantity = instrument.Quantity,
            Unit = instrument.Unit,
            OpenedOn = instrument.OpenedOn,
            ExpiresOn = instrument.ExpiresOn,
            Notes = instrument.Notes
        };
    }

    public sealed class ResolveQrRequest
    {
        public string QrPayload { get; set; } = string.Empty;
    }
}
