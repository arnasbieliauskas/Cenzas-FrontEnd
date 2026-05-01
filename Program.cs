using CenzasBackend.Models;
using CenzasBackend.Services;
using Microsoft.Extensions.Options;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 1. Dynamic Email Configuration (IOptions Pattern)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<ISmtpClientFactory, SmtpClientFactory>();
builder.Services.AddScoped<IEmailService, EmailService>();

// 2. Statistics Cache Configuration
builder.Services.Configure<StatisticsCacheSettings>(builder.Configuration.GetSection("StatisticsCacheSettings"));
builder.Services.Configure<DataSanitizationSettings>(builder.Configuration.GetSection("DataSanitizationSettings"));
builder.Services.AddHostedService<StatisticsCacheWorker>();
builder.Services.AddHostedService<RpaJobWatcher>();

// 2. MySQL Database Connection Configuration
var connectionString = builder.Configuration.GetConnectionString("NtdDatabase");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddMySqlDataSource(connectionString);
    builder.Services.AddSingleton<IDbConnectionFactory, MySqlDbConnectionFactory>();
}

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

// 3. Database Maintenance & Diagnostics
builder.Services.AddScoped<DatabaseMaintenanceService>();
builder.Services.AddScoped<DiagnosticService>();
builder.Services.AddScoped<IMarketAnalyticsService, MarketAnalyticsService>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddScoped<IMetadataGeneratorService, MetadataGeneratorService>();

var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Startup Logging & Checks
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log Email Service Initialization (Recipient Check)
var emailSettings = app.Services.GetRequiredService<IOptions<EmailSettings>>().Value;
logger.LogInformation("Email Service initialized. Recipient: {RecipientEmail}", emailSettings.RecipientEmail);

// Log Database Connection Initialization
if (!string.IsNullOrEmpty(connectionString))
{
    logger.LogInformation("Database connection configured for user: cenzas_user");

    // Startup: Execute Database Maintenance (Rule #16)
    using (var scope = app.Services.CreateScope())
    {
        var maintenance = scope.ServiceProvider.GetRequiredService<DatabaseMaintenanceService>();
        var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataGeneratorService>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            startupLogger.LogInformation("Startup: Verifying DB Schema and Strategic Indexes...");
            await maintenance.EnsureDatabaseSchemaAsync();
            startupLogger.LogInformation("Startup: DB Maintenance completed.");

            startupLogger.LogInformation("Startup: Force-refreshing metadata for frontend stabilization...");
            await metadataService.RefreshMetadataAsync();
        }
        catch (Exception ex)
        {
            startupLogger.LogCritical(ex, "Startup: DB Maintenance failed.");
        }
    }
}
else
{
    logger.LogWarning("Database connection string 'NtdDatabase' is missing!");
}

// Serve Static Files From wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// Ensure Data Directory Exists
var cacheSettings = app.Services.GetRequiredService<IOptions<StatisticsCacheSettings>>().Value;
var fullCachePath = Path.Combine(app.Environment.ContentRootPath, cacheSettings.CacheFilePath);
var cacheDir = Path.GetDirectoryName(fullCachePath);
if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
{
    Directory.CreateDirectory(cacheDir);
    logger.LogInformation("Created data directory: {Directory}", cacheDir);
}

app.UseRouting();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();
