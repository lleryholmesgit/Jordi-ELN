using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Data;

public static class SchemaUpdater
{
    public static async Task EnsureCurrentSchemaAsync(ApplicationDbContext context)
    {
        if (!context.Database.IsSqlite())
        {
            return;
        }

        await EnsureTableAsync(context, "ApplicationSettings", @"
CREATE TABLE IF NOT EXISTS [ApplicationSettings] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_ApplicationSettings] PRIMARY KEY AUTOINCREMENT,
    [Key] TEXT NOT NULL,
    [Value] TEXT NOT NULL
);");
        await EnsureTableAsync(context, "StorageLocations", @"
CREATE TABLE IF NOT EXISTS [StorageLocations] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_StorageLocations] PRIMARY KEY AUTOINCREMENT,
    [Code] TEXT NOT NULL,
    [Name] TEXT NOT NULL,
    [Notes] TEXT NOT NULL DEFAULT '',
    [QrCodeToken] TEXT NOT NULL,
    [CreatedAtUtc] TEXT NOT NULL
);");
        await EnsureIndexAsync(context, "CREATE UNIQUE INDEX IF NOT EXISTS [IX_ApplicationSettings_Key] ON [ApplicationSettings] ([Key]);");
        await EnsureIndexAsync(context, "CREATE UNIQUE INDEX IF NOT EXISTS [IX_StorageLocations_Code] ON [StorageLocations] ([Code]);");
        await EnsureIndexAsync(context, "CREATE UNIQUE INDEX IF NOT EXISTS [IX_StorageLocations_QrCodeToken] ON [StorageLocations] ([QrCodeToken]);");
        await EnsureIndexAsync(context, "CREATE UNIQUE INDEX IF NOT EXISTS [IX_StorageLocations_Name] ON [StorageLocations] ([Name]);");
        await EnsureIndexAsync(context, "DROP INDEX IF EXISTS [IX_ExperimentRecords_ExperimentCode];");
        await EnsureIndexAsync(context, "CREATE UNIQUE INDEX IF NOT EXISTS [IX_ExperimentRecords_ProjectName_Title_ExperimentCode] ON [ExperimentRecords] ([ProjectName], [Title], [ExperimentCode]);");
        await EnsureColumnAsync(context, "Instruments", "ItemType", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(context, "Instruments", "ProductNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "CatalogNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "LotNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "ExpNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "Quantity", "REAL NULL");
        await EnsureColumnAsync(context, "Instruments", "Unit", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "OpenedOn", "REAL NULL");
        await EnsureColumnAsync(context, "Instruments", "ExpiresOn", "REAL NULL");
        await EnsureColumnAsync(context, "Instruments", "StorageLocationId", "INTEGER NULL");
        await EnsureColumnAsync(context, "RecordInstrumentLinks", "UsageHours", "REAL NULL");
        await EnsureColumnAsync(context, "AspNetUsers", "RecoveryQuestion", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "AspNetUsers", "RecoveryAnswerHash", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "RecordTemplates", "Status", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(context, "RecordTemplates", "SubmittedByUserId", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "RecordTemplates", "SubmittedAtUtc", "TEXT NULL");
        await EnsureColumnAsync(context, "RecordTemplates", "ReviewedByUserId", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "RecordTemplates", "ReviewedAtUtc", "TEXT NULL");
        await EnsureColumnAsync(context, "RecordTemplates", "ReviewComment", "TEXT NOT NULL DEFAULT ''");
    }

    private static async Task EnsureTableAsync(ApplicationDbContext context, string tableName, string createSql)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{tableName}';";
            var exists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            if (!exists)
            {
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync(createSql);
#pragma warning restore EF1002
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureIndexAsync(ApplicationDbContext context, string createIndexSql)
    {
#pragma warning disable EF1002
        await context.Database.ExecuteSqlRawAsync(createIndexSql);
#pragma warning restore EF1002
    }

    private static async Task EnsureColumnAsync(ApplicationDbContext context, string tableName, string columnName, string columnDefinition)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info([{tableName}]);";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

#pragma warning disable EF1002
        await context.Database.ExecuteSqlRawAsync($"ALTER TABLE [{tableName}] ADD COLUMN [{columnName}] {columnDefinition};");
#pragma warning restore EF1002
    }
}
