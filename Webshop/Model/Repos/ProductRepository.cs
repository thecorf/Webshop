using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Webshop.Model;

namespace Webshop.Model.Repos;

public sealed class ProductRepository : SqlRepositoryBase, IProductRepository
{
    private const string TableName = "Products";
    private const string IdColumn = "Id";
    private const string NameColumn = "Name";
    private const string CategoryColumn = "Category";
    private const string DescriptionColumn = "Description";
    private const string PriceColumn = "Price";
    private const byte PricePrecision = 18;
    private const byte PriceScale = 2;
    private const int SearchPatternLength = 4000;
    private const int CategoryLength = 100;

    public ProductRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken)
    {
        var sql = $"""
                 SELECT {IdColumn}, {NameColumn}, {CategoryColumn}, {DescriptionColumn}, {PriceColumn}
                 FROM {TableName}
                 ORDER BY {NameColumn};
                 """;

        return await QueryAsync(sql, null, MapProduct, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var sql = $"""
                 SELECT {IdColumn}, {NameColumn}, {CategoryColumn}, {DescriptionColumn}, {PriceColumn}
                 FROM {TableName}
                 WHERE {IdColumn} = @id;
                 """;

        var parameters = new[]
        {
            new SqlParameter("@id", SqlDbType.Int)
            {
                Value = id
            }
        };

        var rows = await QueryAsync(sql, parameters, MapProduct, cancellationToken).ConfigureAwait(false);
        foreach (var product in rows)
        {
            return product;
        }

        return null;
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken)
    {
        var sql = $"""
                 SELECT {IdColumn}, {NameColumn}, {CategoryColumn}, {DescriptionColumn}, {PriceColumn}
                 FROM {TableName}
                 WHERE {CategoryColumn} = @category
                 ORDER BY {NameColumn};
                 """;

        var parameters = new[]
        {
            new SqlParameter("@category", SqlDbType.NVarChar, CategoryLength)
            {
                Value = category ?? string.Empty
            }
        };

        return await QueryAsync(sql, parameters, MapProduct, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Product>> SearchAsync(string searchTerm, decimal? maxPrice, CancellationToken cancellationToken)
    {
        var sql = $"""
                 SELECT {IdColumn}, {NameColumn}, {CategoryColumn}, {DescriptionColumn}, {PriceColumn}
                 FROM {TableName}
                 WHERE {NameColumn} LIKE @pattern ESCAPE '\\'
                    OR {DescriptionColumn} LIKE @pattern ESCAPE '\\'
                 ORDER BY {NameColumn};
                 """;

        if (maxPrice.HasValue)
        {
            sql = $"""
                     SELECT {IdColumn}, {NameColumn}, {CategoryColumn}, {DescriptionColumn}, {PriceColumn}
                     FROM {TableName}
                     WHERE ({NameColumn} LIKE @pattern ESCAPE '\\'
                        OR {DescriptionColumn} LIKE @pattern ESCAPE '\\')
                       AND {PriceColumn} <= @maxPrice
                     ORDER BY {NameColumn};
                     """;
        }

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@pattern", SqlDbType.NVarChar, SearchPatternLength)
            {
                Value = $"%{EscapeLikePattern(searchTerm ?? string.Empty)}%"
            }
        };

        if (maxPrice.HasValue)
        {
            var maxPriceParameter = new SqlParameter("@maxPrice", SqlDbType.Decimal)
            {
                Precision = PricePrecision,
                Scale = PriceScale,
                Value = maxPrice.Value
            };

            parameters.Add(maxPriceParameter);
        }

        return await QueryAsync(sql, parameters, MapProduct, cancellationToken).ConfigureAwait(false);
    }

    private static Product MapProduct(SqlDataReader reader)
    {
        var descriptionOrdinal = reader.GetOrdinal(DescriptionColumn);

        return new Product
        {
            Id = reader.GetInt32(reader.GetOrdinal(IdColumn)),
            Name = reader.GetString(reader.GetOrdinal(NameColumn)),
            Category = reader.GetString(reader.GetOrdinal(CategoryColumn)),
            Description = reader.IsDBNull(descriptionOrdinal) ? null : reader.GetString(descriptionOrdinal),
            Price = reader.GetDecimal(reader.GetOrdinal(PriceColumn))
        };
    }

    private static string EscapeLikePattern(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
