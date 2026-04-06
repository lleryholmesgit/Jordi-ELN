using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Controllers;

[Authorize]
public sealed class InventoryController : Controller
{
    private const string InventoryLayoutSettingKey = "InventoryLayout";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly (string Key, string Label)[] InventoryColumns =
    {
        ("ItemType", "Type"),
        ("Code", "Code"),
        ("Name", "Name"),
        ("Manufacturer", "Manufacturer"),
        ("Location", "Location"),
        ("Status", "Status"),
        ("Model", "Model"),
        ("SerialNumber", "Serial number"),
        ("OwnerName", "Owner"),
        ("CalibrationInfo", "Calibration"),
        ("ProductNumber", "Product number"),
        ("CatalogNumber", "Cat number"),
        ("LotNumber", "Lot number"),
        ("ExpNumber", "Exp number"),
        ("Quantity", "Quantity"),
        ("Unit", "Unit"),
        ("OpenedOn", "Opened on"),
        ("ExpiresOn", "Expiry date"),
        ("Notes", "Notes")
    };

    private readonly IInstrumentService _instrumentService;
    private readonly IQrCodeService _qrCodeService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public InventoryController(IInstrumentService instrumentService, IQrCodeService qrCodeService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _instrumentService = instrumentService;
        _qrCodeService = qrCodeService;
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index(string? q, InstrumentStatus? status, InventoryItemType? itemType, string? sortBy, bool descending, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var inventoryItems = await _instrumentService.SearchAsync(new InstrumentSearchRequest
        {
            Query = q ?? string.Empty,
            Status = status,
            ItemType = itemType,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "Code" : sortBy,
            Descending = descending
        }, cancellationToken);

        ViewBag.Query = q ?? string.Empty;
        ViewBag.Status = status;
        ViewBag.ItemType = itemType;
        ViewBag.SortBy = string.IsNullOrWhiteSpace(sortBy) ? "Code" : sortBy;
        ViewBag.Descending = descending;

        return View(new InventoryIndexViewModel
        {
            Items = inventoryItems,
            Columns = await LoadColumnPreferencesAsync(actorUserId, cancellationToken)
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var inventoryItem = await _instrumentService.GetAsync(id, cancellationToken);
        if (inventoryItem is null)
        {
            return NotFound();
        }

        return View(ToEditorModel(inventoryItem));
    }

    [Authorize(Roles = Roles.Admin)]
    public IActionResult Create(InventoryItemType itemType = InventoryItemType.Instrument)
    {
        return View("Edit", BuildNewInventoryModel(itemType));
    }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var inventoryItem = await _instrumentService.GetAsync(id, cancellationToken);
        return inventoryItem is null ? NotFound() : View(ToEditorModel(inventoryItem));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GenerateQrCode(InstrumentEditorViewModel input)
    {
        input.Code = GenerateInventoryCode(input.ItemType);
        PopulateGeneratedQr(input);
        ModelState.Clear();
        return View("Edit", input);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(InstrumentEditorViewModel input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Code))
        {
            input.Code = GenerateInventoryCode(input.ItemType);
        }

        PopulateGeneratedQr(input);

        if (!ModelState.IsValid)
        {
            return View("Edit", input);
        }

        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var request = ToSaveRequest(input);

        if (input.Id.HasValue)
        {
            var updated = await _instrumentService.UpdateAsync(input.Id.Value, request, actorUserId, "Web", cancellationToken);
            return RedirectToAction(nameof(Details), new { id = updated?.Id ?? input.Id.Value });
        }

        var created = await _instrumentService.CreateAsync(request, actorUserId, "Web", cancellationToken);
        return RedirectToAction(nameof(Details), new { id = created.Id });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var deleted = await _instrumentService.DeleteAsync(id, actorUserId, "Web", cancellationToken);
        if (!deleted)
        {
            TempData["InventoryError"] = "The inventory item could not be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["InventoryMessage"] = "Inventory item deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLayout(List<InventoryColumnPreferenceInput> columns, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Forbid();
        }

        var normalized = GetDefaultColumnPreferences()
            .Select((column, index) =>
            {
                var selected = columns.FirstOrDefault(x => string.Equals(x.Key, column.Key, StringComparison.OrdinalIgnoreCase));
                return new InventoryColumnPreference
                {
                    Key = column.Key,
                    Label = column.Label,
                    Position = selected?.Position ?? index,
                    IsVisible = selected?.IsVisible ?? false
                };
            })
            .OrderBy(x => x.Position)
            .ThenBy(x => x.Label)
            .ToList();

        await SaveColumnPreferencesAsync(actorUserId, normalized, cancellationToken);
        return Ok(new { success = true });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportSpreadsheet(IFormFile? spreadsheet, CancellationToken cancellationToken)
    {
        if (spreadsheet is null || spreadsheet.Length == 0)
        {
            TempData["InventoryError"] = "Please choose a CSV spreadsheet to import.";
            return RedirectToAction(nameof(Index));
        }

        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        var importedCount = 0;

        using var stream = spreadsheet.OpenReadStream();
        using var reader = new StreamReader(stream);
        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            TempData["InventoryError"] = "The spreadsheet is empty.";
            return RedirectToAction(nameof(Index));
        }

        var delimiter = headerLine.Contains('\t') ? '\t' : ',';
        var headers = ParseDelimitedLine(headerLine, delimiter)
            .Select((value, index) => new { Key = value.Trim(), Index = index })
            .ToDictionary(x => x.Key, x => x.Index, StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = ParseDelimitedLine(line, delimiter);
            var request = new InstrumentSaveRequest
            {
                ItemType = ParseEnum(GetValue(headers, columns, "ItemType"), InventoryItemType.Chemical),
                Code = GetValue(headers, columns, "Code"),
                Name = GetValue(headers, columns, "Name"),
                Manufacturer = GetValue(headers, columns, "Manufacturer"),
                Location = GetValue(headers, columns, "Location"),
                Status = ParseEnum(GetValue(headers, columns, "Status"), InstrumentStatus.Active),
                Model = GetValue(headers, columns, "Model"),
                SerialNumber = GetValue(headers, columns, "SerialNumber"),
                OwnerName = GetValue(headers, columns, "OwnerName"),
                CalibrationInfo = GetValue(headers, columns, "CalibrationInfo"),
                ProductNumber = GetValue(headers, columns, "ProductNumber"),
                CatalogNumber = GetValue(headers, columns, "CatalogNumber"),
                LotNumber = GetValue(headers, columns, "LotNumber"),
                ExpNumber = GetValue(headers, columns, "ExpNumber"),
                Quantity = ParseNullableDecimal(GetValue(headers, columns, "Quantity")),
                Unit = GetValue(headers, columns, "Unit"),
                OpenedOn = ParseNullableDate(GetValue(headers, columns, "OpenedOn")),
                ExpiresOn = ParseNullableDate(GetValue(headers, columns, "ExpiresOn")),
                Notes = GetValue(headers, columns, "Notes")
            };

            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
            {
                continue;
            }

            var existing = await _instrumentService.GetByCodeAsync(request.Code, cancellationToken);
            if (existing is null)
            {
                await _instrumentService.CreateAsync(request, actorUserId, "SpreadsheetImport", cancellationToken);
            }
            else
            {
                await _instrumentService.UpdateAsync(existing.Id, request, actorUserId, "SpreadsheetImport", cancellationToken);
            }

            importedCount++;
        }

        TempData["InventoryMessage"] = $"Imported {importedCount} inventory row(s).";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshQr(int id, CancellationToken cancellationToken)
    {
        var actorUserId = _userManager.GetUserId(User) ?? string.Empty;
        await _instrumentService.RefreshQrAsync(id, actorUserId, "Web", cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [AllowAnonymous]
    public IActionResult Scan()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveScan(string qrPayload, CancellationToken cancellationToken)
    {
        var instrument = await _instrumentService.GetByTokenAsync(qrPayload, cancellationToken);
        if (instrument is null)
        {
            TempData["ScanError"] = "The QR code could not be resolved to an active instrument.";
            return RedirectToAction(nameof(Scan));
        }

        return RedirectToAction(nameof(Details), new { id = instrument.Id });
    }

    private InstrumentEditorViewModel ToEditorModel(Instrument instrument)
    {
        return new InstrumentEditorViewModel
        {
            Id = instrument.Id,
            ItemType = instrument.ItemType,
            Code = instrument.Code,
            Name = instrument.Name,
            Model = instrument.Model,
            Manufacturer = instrument.Manufacturer,
            SerialNumber = instrument.SerialNumber,
            Location = instrument.Location,
            Status = instrument.Status,
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
            Notes = instrument.Notes,
            QrCodeToken = instrument.QrCodeToken,
            QrCodeSvg = string.IsNullOrWhiteSpace(instrument.QrCodeToken) ? string.Empty : _qrCodeService.GenerateSvg(instrument.QrCodeToken)
        };
    }

    private static InstrumentSaveRequest ToSaveRequest(InstrumentEditorViewModel input)
    {
        return new InstrumentSaveRequest
        {
            ItemType = input.ItemType,
            Code = input.Code,
            Name = input.Name,
            Model = input.Model,
            Manufacturer = input.Manufacturer,
            SerialNumber = input.SerialNumber,
            Location = input.Location,
            Status = input.Status,
            OwnerName = input.OwnerName,
            CalibrationInfo = input.CalibrationInfo,
            ProductNumber = input.ProductNumber,
            CatalogNumber = input.CatalogNumber,
            LotNumber = input.LotNumber,
            ExpNumber = input.ExpNumber,
            Quantity = input.Quantity,
            Unit = input.Unit,
            OpenedOn = input.OpenedOn,
            ExpiresOn = input.ExpiresOn,
            Notes = input.Notes
        };
    }

    private static string[] ParseDelimitedLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private static string GetValue(IReadOnlyDictionary<string, int> headers, IReadOnlyList<string> columns, string key)
    {
        return headers.TryGetValue(key, out var index) && index < columns.Count ? columns[index].Trim() : string.Empty;
    }

    private static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    }

    private static decimal? ParseNullableDecimal(string value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateOnly? ParseNullableDate(string value)
    {
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private InstrumentEditorViewModel BuildNewInventoryModel(InventoryItemType itemType)
    {
        var model = new InstrumentEditorViewModel
        {
            ItemType = itemType,
            Status = InstrumentStatus.Active,
            Code = GenerateInventoryCode(itemType)
        };

        PopulateGeneratedQr(model);
        return model;
    }

    private void PopulateGeneratedQr(InstrumentEditorViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            model.QrCodeToken = string.Empty;
            model.QrCodeSvg = string.Empty;
            return;
        }

        model.QrCodeToken = _qrCodeService.GenerateToken(model.Code);
        model.QrCodeSvg = _qrCodeService.GenerateSvg(model.QrCodeToken);
    }

    private static string GenerateInventoryCode(InventoryItemType itemType)
    {
        var prefix = itemType == InventoryItemType.Instrument ? "J-INS" : "J-CH";
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        return $"{prefix}-{suffix}";
    }

    private async Task<IReadOnlyList<InventoryColumnPreference>> LoadColumnPreferencesAsync(string actorUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return GetDefaultColumnPreferences();
        }

        var settingKey = GetInventoryLayoutSettingKey(actorUserId);
        var setting = await _context.ApplicationSettings.AsQueryable().FirstOrDefaultAsync(x => x.Key == settingKey, cancellationToken);
        var defaults = GetDefaultColumnPreferences();
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return defaults;
        }

        try
        {
            var saved = JsonSerializer.Deserialize<List<InventoryColumnPreference>>(setting.Value, JsonOptions) ?? new List<InventoryColumnPreference>();
            return defaults
                .Select((column, index) =>
                {
                    var match = saved.FirstOrDefault(x => string.Equals(x.Key, column.Key, StringComparison.OrdinalIgnoreCase));
                    return new InventoryColumnPreference
                    {
                        Key = column.Key,
                        Label = column.Label,
                        Position = match?.Position ?? index,
                        IsVisible = match?.IsVisible ?? true
                    };
                })
                .OrderBy(x => x.Position)
                .ThenBy(x => x.Label)
                .ToList();
        }
        catch
        {
            return defaults;
        }
    }

    private async Task SaveColumnPreferencesAsync(string actorUserId, IReadOnlyList<InventoryColumnPreference> columns, CancellationToken cancellationToken)
    {
        var settingKey = GetInventoryLayoutSettingKey(actorUserId);
        var serialized = JsonSerializer.Serialize(columns, JsonOptions);
        var setting = await _context.ApplicationSettings.AsQueryable().FirstOrDefaultAsync(x => x.Key == settingKey, cancellationToken);
        if (setting is null)
        {
            setting = new ApplicationSetting { Key = settingKey, Value = serialized };
            _context.ApplicationSettings.Add(setting);
        }
        else
        {
            setting.Value = serialized;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string GetInventoryLayoutSettingKey(string actorUserId)
    {
        return $"{InventoryLayoutSettingKey}:{actorUserId}";
    }

    private static IReadOnlyList<InventoryColumnPreference> GetDefaultColumnPreferences()
    {
        return InventoryColumns
            .Select((column, index) => new InventoryColumnPreference
            {
                Key = column.Key,
                Label = column.Label,
                Position = index,
                IsVisible = true
            })
            .ToList();
    }
}
