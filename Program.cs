using ElectronicLabNotebook.Data;
using ElectronicLabNotebook.Models;
using ElectronicLabNotebook.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

static bool HasConfiguredHttpsEndpoint(IConfiguration configuration)
{
    var urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"] ?? string.Empty;
    if (urls.Contains("https://", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!string.IsNullOrWhiteSpace(configuration["HTTPS_PORT"]) ||
        !string.IsNullOrWhiteSpace(configuration["ASPNETCORE_HTTPS_PORT"]))
    {
        return true;
    }

    return configuration
        .GetSection("Kestrel:Endpoints")
        .GetChildren()
        .Any(endpoint => (endpoint["Url"] ?? string.Empty).Contains("https://", StringComparison.OrdinalIgnoreCase));
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("ElectronicLabNotebook");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IRecordService, RecordService>();
builder.Services.AddScoped<IInstrumentService, InstrumentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (HasConfiguredHttpsEndpoint(builder.Configuration))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inventory}/{action=Index}/{id?}");
app.MapRazorPages();

await SeedData.InitializeAsync(app.Services);

app.Run();
