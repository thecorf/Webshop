using Microsoft.Data.SqlClient;

namespace Webshop.Model.Repos;

/// <summary>
/// Basen for alle ADO.NET repositories.
/// Choke point for parameteriserede queries (SQL injection-beskyttelse).
/// </summary>
public abstract class SqlRepositoryBase
{
    protected readonly IDbConnectionFactory _factory;

    protected SqlRepositoryBase(IDbConnectionFactory factory) => _factory = factory;


    protected async Task<List<T>> QueryAsync<T>(
        string sql, Func<SqlDataReader, T> map,
        Action<SqlParameterCollection>? bind = null,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        bind?.Invoke(cmd.Parameters);

        var results = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(map(reader));
        return results;
    }

    protected async Task<int> ExecuteAsync(
        string sql,
        Action<SqlParameterCollection>? bind = null,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        bind?.Invoke(cmd.Parameters);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    protected async Task<T?> ScalarAsync<T>(
        string sql,
        Action<SqlParameterCollection>? bind = null,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        bind?.Invoke(cmd.Parameters);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is DBNull || result == null)
            return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }


    protected Task<List<T>> QueryAsync<T>(
        string sql, IEnumerable<SqlParameter>? parameters,
        Func<SqlDataReader, T> map, CancellationToken ct = default)
        => QueryAsync<T>(sql, map,
            p => { if (parameters != null) foreach (var par in parameters) p.Add(par); },
            ct);

    protected Task<int> ExecuteAsync(
        string sql, IEnumerable<SqlParameter>? parameters,
        CancellationToken ct = default)
        => ExecuteAsync(sql,
            p => { if (parameters != null) foreach (var par in parameters) p.Add(par); },
            ct);

    protected Task<T?> ScalarAsync<T>(
        string sql, IEnumerable<SqlParameter>? parameters,
        CancellationToken ct = default)
        => ScalarAsync<T>(sql,
            p => { if (parameters != null) foreach (var par in parameters) p.Add(par); },
            ct);


    protected async Task<T> InTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> work,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            var result = await work(conn, tx);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}