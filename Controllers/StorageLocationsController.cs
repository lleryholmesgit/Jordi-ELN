using System.Security.Cryptography;
using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using ElectronicLabNotebook.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Controllers;

[Authorize]
public sealed class StorageLocationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeService _qrCodeService;

    public StorageLocationsController(ApplicationDbContext context, IQrCodeService qrCodeService)
    {
        _context = context;
        _qrCodeService = qrCodeService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var locations = await _context.StorageLocations
            .Include(x => x.InventoryItems)
            .OrderBy(x => x.Name)
            .Select(x => new StorageLocationSummaryViewModel
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Notes = x.Notes,
                InventoryItemCount = x.InventoryItems.Count
            })
            .ToListAsync(cancellationToken);

        return View(new StorageLocationIndexViewModel
        {
            Locations = locations
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var location = await _context.StorageLocations
            .Include(x => x.InventoryItems.OrderBy(item => item.ItemType).ThenBy(item => item.Code))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        return View(await ToEditorViewModelAsync(location, cancellationToken));
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    public IActionResult Create()
    {
        var model = new StorageLocationEditorViewModel
        {
            Code = GenerateStorageLocationCode()
        };
        PopulateGeneratedQr(model);
        return View("Edit", model);
    }

    [Authorize(Roles = Roles.Admin)]
    [WindowsWriteAccess]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var location = await _context.StorageLocations
            .Include(x => x.InventoryItems.OrderBy(item => item.ItemType).ThenBy(item => item.Code))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }

        return View(await ToEditorViewModelAsync(location, cancellationToken));
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public IActionResult GenerateQrCode(StorageLocationEditorViewModel model)
    {
        model.Code = GenerateStorageLocationCode();
        PopulateGeneratedQr(model);
        ModelState.Clear();
        return View("Edit", model);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Save(StorageLocationEditorViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            model.Code = GenerateStorageLocationCode();
        }

        PopulateGeneratedQr(model);

        if (!ModelState.IsValid)
        {
            return View("Edit", model);
        }

        StorageLocation entity;
        if (model.Id.HasValue)
        {
            entity = await _context.StorageLocations.FirstOrDefaultAsync(x => x.Id == model.Id.Value, cancellationToken) ?? throw new InvalidOperationException("Storage location was not found.");
        }
        else
        {
            entity = new StorageLocation();
            _context.StorageLocations.Add(entity);
        }

        entity.Code = model.Code.Trim();
        entity.Name = model.Name.Trim();
        entity.Notes = model.Notes.Trim();
        entity.QrCodeToken = model.QrCodeToken;

        await _context.SaveChangesAsync(cancellationToken);

        var linkedItems = await _context.Instruments
            .Where(x => x.StorageLocationId == entity.Id)
            .ToListAsync(cancellationToken);
        foreach (var item in linkedItems)
        {
            item.Location = entity.Name;
        }

        await _context.SaveChangesAsync(cancellationToken);

        TempData["StorageLocationMessage"] = "Storage location saved.";
        return RedirectToAction(nameof(Details), new { id = entity.Id });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> AddInventoryItems(int id, List<int> selectedInventoryItemIds, CancellationToken cancellationToken)
    {
        var location = await _context.StorageLocations
            .Include(x => x.InventoryItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (location is null)
        {
            TempData["StorageLocationError"] = "The selected storage location could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var ids = selectedInventoryItemIds.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["StorageLocationError"] = "Select at least one inventory item to add.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var items = await _context.Instruments
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.StorageLocationId = location.Id;
            item.Location = location.Name;
        }

        await _context.SaveChangesAsync(cancellationToken);
        TempData["StorageLocationMessage"] = $"Added {items.Count} inventory item(s) to {location.Name}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [WindowsWriteAccess]
    public async Task<IActionResult> Delete(int id, int? replacementStorageLocationId, CancellationToken cancellationToken)
    {
        var location = await _context.StorageLocations
            .Include(x => x.InventoryItems)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (location is null)
        {
            TempData["StorageLocationError"] = "The selected storage location could not be found.";
            return RedirectToAction(nameof(Index));
        }

        if (location.InventoryItems.Count > 0)
        {
            if (!replacementStorageLocationId.HasValue || replacementStorageLocationId.Value == id)
            {
                TempData["StorageLocationError"] = "Choose a new storage location for the inventory items before deleting this storage location.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var replacement = await _context.StorageLocations.FirstOrDefaultAsync(x => x.Id == replacementStorageLocationId.Value, cancellationToken);
            if (replacement is null)
            {
                TempData["StorageLocationError"] = "The replacement storage location could not be found.";
                return RedirectToAction(nameof(Details), new { id });
            }

            foreach (var item in location.InventoryItems)
            {
                item.StorageLocationId = replacement.Id;
                item.Location = replacement.Name;
            }
        }

        _context.StorageLocations.Remove(location);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["StorageLocationMessage"] = "Storage location deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<StorageLocationEditorViewModel> ToEditorViewModelAsync(StorageLocation location, CancellationToken cancellationToken)
    {
        var availableInventoryOptions = await _context.Instruments
            .Where(x => x.StorageLocationId != location.Id)
            .OrderBy(x => x.ItemType)
            .ThenBy(x => x.Code)
            .Select(x => new SelectListItem($"[{x.ItemType}] {x.Code} - {x.Name}", x.Id.ToString()))
            .ToListAsync(cancellationToken);

        var replacementLocationOptions = await _context.StorageLocations
            .Where(x => x.Id != location.Id)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} ({x.Code})", x.Id.ToString()))
            .ToListAsync(cancellationToken);

        return new StorageLocationEditorViewModel
        {
            Id = location.Id,
            Code = location.Code,
            Name = location.Name,
            Notes = location.Notes,
            QrCodeToken = location.QrCodeToken,
            QrCodeSvg = string.IsNullOrWhiteSpace(location.QrCodeToken) ? string.Empty : _qrCodeService.GenerateSvg(location.QrCodeToken),
            InventoryItems = location.InventoryItems.OrderBy(x => x.ItemType).ThenBy(x => x.Code).ToList(),
            AvailableInventoryOptions = availableInventoryOptions,
            ReplacementLocationOptions = replacementLocationOptions
        };
    }

    private void PopulateGeneratedQr(StorageLocationEditorViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Code))
        {
            model.QrCodeToken = string.Empty;
            model.QrCodeSvg = string.Empty;
            return;
        }

        model.QrCodeToken = _qrCodeService.GenerateStorageLocationToken(model.Code);
        model.QrCodeSvg = _qrCodeService.GenerateSvg(model.QrCodeToken);
    }

    private static string GenerateStorageLocationCode()
    {
        return $"J-STO-{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}";
    }
}
