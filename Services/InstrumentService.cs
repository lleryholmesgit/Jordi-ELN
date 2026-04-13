using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Services;

public sealed class InstrumentService : IInstrumentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

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
        var query = _context.Instruments
            .Include(x => x.StorageLocation)
            .AsNoTracking()
            .AsQueryable();

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
        return _context.Instruments
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<Instrument?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _context.Instruments
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
    }

    public async Task<Instrument?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!_qrCodeService.TryParseToken(token, out var code))
        {
            return null;
        }

        return await _context.Instruments
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(x => x.Code == code && x.Status == InstrumentStatus.Active, cancellationToken);
    }

    public async Task<Instrument> CreateAsync(InstrumentSaveRequest request, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = new Instrument();
        await ApplyAsync(entity, request, cancellationToken);
        entity.QrCodeToken = _qrCodeService.GenerateToken(entity.Code);

        _context.Instruments.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.WriteAsync("InventoryItemCreated", nameof(Instrument), entity.Id.ToString(), actorUserId, sourceClient, string.Empty, JsonSerializer.Serialize(entity, JsonOptions));
        return entity;
    }

    public async Task<Instrument?> UpdateAsync(int id, InstrumentSaveRequest request, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Instruments
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);
        await ApplyAsync(entity, request, cancellationToken);
        entity.QrCodeToken = _qrCodeService.GenerateToken(entity.Code);

        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("InventoryItemUpdated", nameof(Instrument), entity.Id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity, JsonOptions));
        return entity;
    }

    public async Task<Instrument?> RefreshQrAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Instruments
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);
        entity.QrCodeToken = _qrCodeService.GenerateToken(entity.Code);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("InventoryItemQrRefreshed", nameof(Instrument), entity.Id.ToString(), actorUserId, sourceClient, beforeJson, JsonSerializer.Serialize(entity, JsonOptions));
        return entity;
    }

    public async Task<bool> DeleteAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Instruments
            .Include(x => x.RecordLinks)
            .Include(x => x.StorageLocation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var beforeJson = JsonSerializer.Serialize(entity, JsonOptions);
        _context.RecordInstrumentLinks.RemoveRange(entity.RecordLinks);
        _context.Instruments.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.WriteAsync("InventoryItemDeleted", nameof(Instrument), id.ToString(), actorUserId, sourceClient, beforeJson, string.Empty);
        return true;
    }

    private async Task ApplyAsync(Instrument entity, InstrumentSaveRequest request, CancellationToken cancellationToken)
    {
        entity.ItemType = request.ItemType;
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.Manufacturer = request.Manufacturer;
        entity.Status = request.Status;
        entity.Notes = request.Notes;

        if (request.ItemType == InventoryItemType.Instrument)
        {
            entity.Model = request.Model;
            entity.SerialNumber = request.SerialNumber;
            entity.OwnerName = string.Empty;
            entity.CalibrationInfo = request.CalibrationInfo;
            entity.ProductNumber = request.ProductNumber;
            entity.CatalogNumber = request.CatalogNumber;
            entity.LotNumber = request.LotNumber;
            entity.ExpNumber = string.Empty;
            entity.Quantity = request.Quantity;
            entity.Unit = request.Unit;
            entity.OpenedOn = null;
            entity.ExpiresOn = null;
        }
        else if (request.ItemType == InventoryItemType.Chemical)
        {
            entity.Model = string.Empty;
            entity.SerialNumber = string.Empty;
            entity.OwnerName = string.Empty;
            entity.CalibrationInfo = string.Empty;
            entity.ProductNumber = request.ProductNumber;
            entity.CatalogNumber = request.CatalogNumber;
            entity.LotNumber = request.LotNumber;
            entity.ExpNumber = request.ExpNumber;
            entity.Quantity = request.Quantity;
            entity.Unit = request.Unit;
            entity.OpenedOn = request.OpenedOn;
            entity.ExpiresOn = request.ExpiresOn;
        }
        else
        {
            entity.Model = request.Model;
            entity.SerialNumber = request.SerialNumber;
            entity.OwnerName = request.OwnerName;
            entity.CalibrationInfo = request.CalibrationInfo;
            entity.ProductNumber = request.ProductNumber;
            entity.CatalogNumber = request.CatalogNumber;
            entity.LotNumber = request.LotNumber;
            entity.ExpNumber = request.ExpNumber;
            entity.Quantity = request.Quantity;
            entity.Unit = request.Unit;
            entity.OpenedOn = request.OpenedOn;
            entity.ExpiresOn = request.ExpiresOn;
        }

        await SyncStorageLocationAsync(entity, request, cancellationToken);
    }

    private async Task SyncStorageLocationAsync(Instrument entity, InstrumentSaveRequest request, CancellationToken cancellationToken)
    {
        StorageLocation? storageLocation = null;

        if (request.StorageLocationId.HasValue)
        {
            storageLocation = await _context.StorageLocations.FirstOrDefaultAsync(x => x.Id == request.StorageLocationId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.Location))
        {
            storageLocation = await EnsureStorageLocationAsync(request.Location.Trim(), cancellationToken);
        }

        entity.StorageLocationId = storageLocation?.Id;
        entity.StorageLocation = storageLocation;
        entity.Location = storageLocation?.Name ?? request.Location.Trim();
    }

    private async Task<StorageLocation> EnsureStorageLocationAsync(string name, CancellationToken cancellationToken)
    {
        var existing = await _context.StorageLocations.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var location = new StorageLocation
        {
            Name = name,
            Code = GenerateStorageLocationCode(),
            QrCodeToken = string.Empty
        };
        location.QrCodeToken = _qrCodeService.GenerateStorageLocationToken(location.Code);

        _context.StorageLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);
        return location;
    }

    private static IEnumerable<Instrument> ApplySorting(IEnumerable<Instrument> query, InstrumentSearchRequest request)
    {
        var descending = request.Descending;
        return request.SortBy switch
        {
            "ItemType" => descending ? query.OrderByDescending(x => x.ItemType).ThenBy(x => x.Code) : query.OrderBy(x => x.ItemType).ThenBy(x => x.Code),
            "Name" => descending ? query.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "Manufacturer" => descending ? query.OrderByDescending(x => x.Manufacturer, StringComparer.OrdinalIgnoreCase) : query.OrderBy(x => x.Manufacturer, StringComparer.OrdinalIgnoreCase),
            "Location" => descending ? query.OrderByDescending(GetStorageLocationName, StringComparer.OrdinalIgnoreCase) : query.OrderBy(GetStorageLocationName, StringComparer.OrdinalIgnoreCase),
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
        yield return GetStorageLocationName(instrument);
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

    private static string GetStorageLocationName(Instrument instrument)
    {
        return instrument.StorageLocation?.Name ?? instrument.Location ?? string.Empty;
    }

    private static string GenerateStorageLocationCode()
    {
        return $"J-STO-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";
    }
}
