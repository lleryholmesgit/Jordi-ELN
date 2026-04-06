using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Data;

public static class SchemaUpdater
{
    public static async Task EnsureCurrentSchemaAsync(ApplicationDbContext context)
    {
        await EnsureTableAsync(context, "ApplicationSettings", @"
CREATE TABLE IF NOT EXISTS [ApplicationSettings] (
    [Id] INTEGER NOT NULL CONSTRAINT [PK_ApplicationSettings] PRIMARY KEY AUTOINCREMENT,
    [Key] TEXT NOT NULL,
    [Value] TEXT NOT NULL
);");
        await EnsureIndexAsync(context, "CREATE UNIQUE INDEX IF NOT EXISTS [IX_ApplicationSettings_Key] ON [ApplicationSettings] ([Key]);");
        await EnsureColumnAsync(context, "Instruments", "ItemType", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(context, "Instruments", "ProductNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "CatalogNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "LotNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "ExpNumber", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "Quantity", "REAL NULL");
        await EnsureColumnAsync(context, "Instruments", "Unit", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(context, "Instruments", "OpenedOn", "REAL NULL");
        await EnsureColumnAsync(context, "Instruments", "ExpiresOn", "REAL NULL");
        await EnsureColumnAsync(context, "RecordInstrumentLinks", "UsageHours", "REAL NULL");
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
