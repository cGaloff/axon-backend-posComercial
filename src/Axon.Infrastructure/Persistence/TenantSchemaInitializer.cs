using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Axon.Infrastructure.Persistence;

public partial class TenantSchemaInitializer
{
    private readonly AppDbContext _dbContext;

    public TenantSchemaInitializer(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InitializeSchemaAsync(string schemaName)
    {
        if (!ValidSchemaNameRegex().IsMatch(schemaName))
        {
            throw new InvalidOperationException($"Invalid tenant schema name: '{schemaName}'.");
        }

        await _dbContext.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"");

        var templateSql = await ReadEmbeddedScriptAsync("tenant_schema_template.sql");
        await _dbContext.Database.ExecuteSqlRawAsync(templateSql.Replace("{SCHEMA_NAME}", schemaName));

        var seedSql = await ReadEmbeddedScriptAsync("tenant_seed.sql");
        await _dbContext.Database.ExecuteSqlRawAsync(seedSql.Replace("{SCHEMA_NAME}", schemaName));
    }

    private static async Task<string> ReadEmbeddedScriptAsync(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource '{fileName}' not found.");

        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidSchemaNameRegex();
}
