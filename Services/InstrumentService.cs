using System.Text.Json;
using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Services;

public sealed class InstrumentService : IInstrumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeService _qrCodeService;
    private readonly IAuditService _auditService;

    public InstrumentService(ApplicationDbContext context, IQrCodeService qrCodeService, IAuditService auditService)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<Instrument>> SearchAsync(InstrumentSearchRequest request, CancellationToken cancellationToken = default)
    {
        var query = _context.Instruments.AsNoTracking().AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (request.ItemType.HasValue)
        {
            query = query.Where(x => x.ItemType == request.ItemType.Value);
        }

        var items = await query.ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            items = items
                .Where(x => MatchesSearch(x, request.Query))
                .ToList();
        }

        return ApplySorting(items, request).ToList();
    }

    public Task<Instrument?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Instruments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Instrument?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _context.Instruments.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
    }

    public async Task<Instrument?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!_qrCodeService.TryParseToken(token, out var code))
        {
            return null;
        }

        return await _context.Instruments.FirstOrDefaultAsync(x => x.Code == code && x.ItemType == InventoryItemType.Instrument && x.Status == InstrumentStatus.Active, cancellationToken);
    }

    public async Task<Instrument> CreateAsync(InstrumentSaveRequest request, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = new Instrument();
        Apply(entity, request);
        entity.QrCodeToken = _qrCodeService.GenerateToken(entity.Code);

        _context.Instruments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync("InventoryItemCreated", nameof(Instrument), entity.Id.ToString(), actorUserId, sourceClient, string.Empty, JsonSerializer.Serialize(entity));
        return entity;
    }

    public async Task<Instrument?> UpdateAsync(int id, InstrumentSaveRequest request, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var beforeJson = JsonSerializer.Serialize(entity);
        Apply(entity, request);
        entity.QrCodeToken = _qrCodeService.GenerateToken(entity.Code);

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("InventoryItemUpdated", nameof(Instrument), entity.Id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity));
        return entity;
    }

    public async Task<Instrument?> RefreshQrAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Instruments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var beforeJson = JsonSerializer.Serialize(entity);
        entity.QrCodeToken = _qrCodeService.GenerateToken(entity.Code);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("InventoryItemQrRefreshed", nameof(Instrument), entity.Id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity));
        return entity;
    }

    public async Task<bool> DeleteAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Instruments
            .Include(x => x.RecordLinks)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var beforeJson = JsonSerializer.Serialize(entity);
        _context.RecordInstrumentLinks.RemoveRange(entity.RecordLinks);
        _context.Instruments.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("InventoryItemDeleted", nameof(Instrument), id.ToString(), actorUserId, sourceClient, beforeJson, string.Empty);
        return true;
    }

    private static IEnumerable<Instrument> ApplySorting(IEnumerable<Instrument> query, InstrumentSearchRequest request)
    {
        var descending = request.Descending;
        return request.SortBy switch
        {
            "ItemType" => descending ? query.OrderByDescending(x => x.ItemType).ThenBy(x => x.Code) : query.OrderBy(x => x.ItemType).ThenBy(x => x.Code),
            "Name" => descending ? query.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "Manufacturer" => descending ? query.OrderByDescending(x => x.Manufacturer, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Manufacturer, StringComparer.OrdinalIgnoreCase),
            "Location" => descending ? query.OrderByDescending(x => x.Location, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Location, StringComparer.OrdinalIgnoreCase),
            "Status" => descending ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "Model" => descending ? query.OrderByDescending(x => x.Model, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Model, StringComparer.OrdinalIgnoreCase),
            "SerialNumber" => descending ? query.OrderByDescending(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase),
            "OwnerName" => descending ? query.OrderByDescending(x => x.OwnerName, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.OwnerName, StringComparer.OrdinalIgnoreCase),
            "CalibrationInfo" => descending ? query.OrderByDescending(x => x.CalibrationInfo, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.CalibrationInfo, StringComparer.OrdinalIgnoreCase),
            "ProductNumber" => descending ? query.OrderByDescending(x => x.ProductNumber, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.ProductNumber, StringComparer.OrdinalIgnoreCase),
            "CatalogNumber" => descending ? query.OrderByDescending(x => x.CatalogNumber, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.CatalogNumber, StringComparer.OrdinalIgnoreCase),
            "LotNumber" => descending ? query.OrderByDescending(x => x.LotNumber, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.LotNumber, StringComparer.OrdinalIgnoreCase),
            "ExpNumber" => descending ? query.OrderByDescending(x => x.ExpNumber, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.ExpNumber, StringComparer.OrdinalIgnoreCase),
            "Quantity" => descending ? query.OrderByDescending(x => x.Quantity) : query.OrderBy(x => x.Quantity),
            "Unit" => descending ? query.OrderByDescending(x => x.Unit, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Unit, StringComparer.OrdinalIgnoreCase),
            "OpenedOn" => descending ? query.OrderByDescending(x => x.OpenedOn) : query.OrderBy(x => x.OpenedOn),
            "ExpiresOn" => descending ? query.OrderByDescending(x => x.ExpiresOn) : query.OrderBy(x => x.ExpiresOn),
            "Notes" => descending ? query.OrderByDescending(x => x.Notes, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Notes, StringComparer.OrdinalIgnoreCase),
            _ => descending ? query.OrderByDescending(x => x.Code, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static bool MatchesSearch(Instrument instrument, string query)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return true;
        }

        return GetSearchFields(instrument)
            .Any(value => value.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetSearchFields(Instrument instrument)
    {
        yield return instrument.ItemType.ToString();
        yield return instrument.Code;
        yield return instrument.Name;
        yield return instrument.Model;
        yield return instrument.Manufacturer;
        yield return instrument.SerialNumber;
        yield return instrument.Location;
        yield return instrument.Status.ToString();
        yield return instrument.OwnerName;
        yield return instrument.CalibrationInfo;
        yield return instrument.ProductNumber;
        yield return instrument.CatalogNumber;
        yield return instrument.LotNumber;
        yield return instrument.ExpNumber;
        yield return instrument.Quantity?.ToString() ?? string.Empty;
        yield return instrument.Unit;
        yield return instrument.OpenedOn?.ToString("yyyy-MM-dd") ?? string.Empty;
        yield return instrument.ExpiresOn?.ToString("yyyy-MM-dd") ?? string.Empty;
        yield return instrument.Notes;
        yield return instrument.QrCodeToken;
    }

    private static void Apply(Instrument entity, InstrumentSaveRequest request)
    {
        entity.ItemType = request.ItemType;
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Model = request.ItemType == InventoryItemType.Instrument ? request.Model : string.Empty;
        entity.Manufacturer = request.Manufacturer;
        entity.SerialNumber = request.ItemType == InventoryItemType.Instrument ? request.SerialNumber : string.Empty;
        entity.Location = request.Location;
        entity.Status = request.Status;
        entity.OwnerName = request.ItemType == InventoryItemType.Instrument ? request.OwnerName : string.Empty;
        entity.CalibrationInfo = request.ItemType == InventoryItemType.Instrument ? request.CalibrationInfo : string.Empty;
        entity.ProductNumber = request.ItemType == InventoryItemType.Chemical ? request.ProductNumber : string.Empty;
        entity.CatalogNumber = request.ItemType == InventoryItemType.Chemical ? request.CatalogNumber : string.Empty;
        entity.LotNumber = request.ItemType == InventoryItemType.Chemical ? request.LotNumber : string.Empty;
        entity.ExpNumber = request.ItemType == InventoryItemType.Chemical ? request.ExpNumber : string.Empty;
        entity.Quantity = request.ItemType == InventoryItemType.Chemical ? request.Quantity : null;
        entity.Unit = request.ItemType == InventoryItemType.Chemical ? request.Unit : string.Empty;
        entity.OpenedOn = request.ItemType == InventoryItemType.Chemical ? request.OpenedOn : null;
        entity.ExpiresOn = request.ItemType == InventoryItemType.Chemical ? request.ExpiresOn : null;
        entity.Notes = request.Notes;
    }
}
