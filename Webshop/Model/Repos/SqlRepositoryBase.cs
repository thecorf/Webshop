using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
namespace Webshop.Model.Repos;

public abstract class SqlRepositoryBase
{
    protected readonly IDbConnectionFactory ConnectionFactory;

    protected SqlRepositoryBase(IDbConnectionFactory connectionFactory)
    {
        ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    protected async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, IEnumerable<SqlParameter>? parameters, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL must not be empty.", nameof(sql));
        }

        ArgumentNullException.ThrowIfNull(map);

        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text
        };

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(map(reader));
        }

        return results;
    }

    protected async Task<int> ExecuteAsync(string sql, IEnumerable<SqlParameter>? parameters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL must not be empty.", nameof(sql));
        }

        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text
        };

        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    protected async Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await work(connection, transaction, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return result;
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Ignore rollback failures and preserve the original exception.
            }

            throw;
        }
    }
}
