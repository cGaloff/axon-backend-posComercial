using System.Data.Common;
using System.Text.RegularExpressions;
using Axon.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Axon.Infrastructure.Persistence.Interceptors;

public partial class TenantSchemaInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantSchemaInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSearchPathAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetSearchPathAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    private async Task SetSearchPathAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var schemaName = _tenantContext.SchemaName;

        if (!ValidSchemaNameRegex().IsMatch(schemaName))
        {
            throw new InvalidOperationException($"Invalid tenant schema name: '{schemaName}'.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SET search_path TO \"{schemaName}\", public";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex ValidSchemaNameRegex();
}
