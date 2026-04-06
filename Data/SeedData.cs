using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<ApplicationDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var qrCodeService = provider.GetRequiredService<IQrCodeService>();

        await context.Database.EnsureCreatedAsync();
        await SchemaUpdater.EnsureCurrentSchemaAsync(context);

        foreach (var role in new[] { Roles.Admin, Roles.Researcher, Roles.Reviewer })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@lab.local", "Admin User", Roles.Admin);
        await EnsureUserAsync(userManager, "researcher@lab.local", "Researcher User", Roles.Researcher);
        await EnsureUserAsync(userManager, "reviewer@lab.local", "Reviewer User", Roles.Reviewer);

        if (!await context.RecordTemplates.AnyAsync())
        {
            context.RecordTemplates.AddRange(
                new RecordTemplate
                {
                    Name = "Analytical Test Template",
                    Description = "Reusable template for standard laboratory analytical runs.",
                    DefaultRichText = "<p>Objective, method, observations, and conclusion.</p>",
                    DefaultStructuredDataJson = "{\"objective\":\"\",\"sampleId\":\"\",\"operator\":\"\"}"
                },
                new RecordTemplate
                {
                    Name = "Freeform Research Note",
                    Description = "Flexible note structure for exploratory experiments."
                });
        }

        if (!await context.Instruments.AnyAsync(x => x.Code == "HPLC-001"))
        {
            var seedInstrument = new Instrument
            {
                ItemType = InventoryItemType.Instrument,
                Code = "HPLC-001",
                Name = "High Performance Liquid Chromatograph",
                Model = "Alliance 2695",
                Manufacturer = "Waters",
                SerialNumber = "WAT-2695-001",
                Location = "Lab A / Bench 2",
                OwnerName = "Core Analytics",
                CalibrationInfo = "Calibrated through 2026-12-31",
                Notes = "Seed instrument for QR workflow demos."
            };

            seedInstrument.QrCodeToken = qrCodeService.GenerateToken(seedInstrument.Code);
            context.Instruments.Add(seedInstrument);
        }

        if (!await context.Instruments.AnyAsync(x => x.Code == "CHEM-ACN-001"))
        {
            var seedChemical = new Instrument
            {
                ItemType = InventoryItemType.Chemical,
                Code = "CHEM-ACN-001",
                Name = "Acetonitrile",
                Manufacturer = "Sigma-Aldrich",
                ProductNumber = "34998",
                CatalogNumber = "ACN-HPLC",
                LotNumber = "LOT-240315",
                ExpNumber = "EXP-2027-03",
                Quantity = 4,
                Unit = "L",
                OpenedOn = new DateOnly(2026, 3, 1),
                ExpiresOn = new DateOnly(2027, 3, 31),
                Location = "Chemical Storage / Shelf 4",
                Notes = "Seed chemical inventory item."
            };

            seedChemical.QrCodeToken = qrCodeService.GenerateToken(seedChemical.Code);
            context.Instruments.Add(seedChemical);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true,
                Department = "Laboratory"
            };

            var result = await userManager.CreateAsync(user, "LabNotebook1");
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
