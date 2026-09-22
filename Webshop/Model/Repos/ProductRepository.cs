using Microsoft.Data.SqlClient;
using Webshop.Model;

namespace Webshop.Model.Repos;

public class ProductRepository : SqlRepositoryBase, IProductRepository
{
    public ProductRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
        => await QueryAsync(
            "SELECT Id, Name, Description, Price, Stock, CategoryId, ImageUrl FROM Products ORDER BY Name",
            Map,
            null,
            ct);

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var products = await QueryAsync(
            "SELECT Id, Name, Description, Price, Stock, CategoryId, ImageUrl FROM Products WHERE Id = @id",
            Map,
            p => p.AddWithValue("@id", id),
            ct);

        return products.FirstOrDefault();
    }

    public async Task<List<Product>> GetByCategoryAsync(int categoryId, CancellationToken ct = default)
        => await QueryAsync(
            "SELECT Id, Name, Description, Price, Stock, CategoryId, ImageUrl FROM Products WHERE CategoryId = @categoryId ORDER BY Name",
            Map,
            p => p.AddWithValue("@categoryId", categoryId),
            ct);

    public async Task<List<Product>> SearchAsync(string searchTerm, CancellationToken ct = default)
    {
        var escaped = searchTerm
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return await QueryAsync(
            @"SELECT Id, Name, Description, Price, Stock, CategoryId, ImageUrl 
              FROM Products 
              WHERE Name LIKE @term ESCAPE '\\' 
                 OR Description LIKE @term ESCAPE '\\'
              ORDER BY Name",
            Map,
            p => p.AddWithValue("@term", $"%{escaped}%"),
            ct);
    }

    public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Products (Name, Description, Price, Stock, CategoryId, ImageUrl)
                             VALUES (@name, @desc, @price, @stock, @categoryId, @imageUrl);
                             SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var newId = await ScalarAsync<int>(
            sql,
            p =>
            {
                p.AddWithValue("@name", product.Name);
                p.AddWithValue("@desc", (object?)product.Description ?? DBNull.Value);
                p.AddWithValue("@price", product.Price);
                p.AddWithValue("@stock", product.Stock);
                p.AddWithValue("@categoryId", product.CategoryId);
                p.AddWithValue("@imageUrl", (object?)product.ImageUrl ?? DBNull.Value);
            },
            ct);

        product.Id = newId;
        return product;
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Products SET
                                Name = @name,
                                Description = @desc,
                                Price = @price,
                                Stock = @stock,
                                CategoryId = @categoryId,
                                ImageUrl = @imageUrl
                             WHERE Id = @id";

        await ExecuteAsync(sql,
            p =>
            {
                p.AddWithValue("@id", product.Id);
                p.AddWithValue("@name", product.Name);
                p.AddWithValue("@desc", (object?)product.Description ?? DBNull.Value);
                p.AddWithValue("@price", product.Price);
                p.AddWithValue("@stock", product.Stock);
                p.AddWithValue("@categoryId", product.CategoryId);
                p.AddWithValue("@imageUrl", (object?)product.ImageUrl ?? DBNull.Value);
            },
            ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM Products WHERE Id = @id";
        await ExecuteAsync(sql, p => p.AddWithValue("@id", id), ct);
    }

    public async Task<Dictionary<int, string>> GetCategoryMapAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<int, string>();

        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        await using var cmd = new SqlCommand("SELECT Id, Name FROM Categories ORDER BY Name", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
            map.Add(reader.GetInt32(0), reader.GetString(1));

        return map;
    }

    private static Product Map(SqlDataReader r) => new()
    {
        Id = r.GetInt32(r.GetOrdinal("Id")),
        Name = r.GetString(r.GetOrdinal("Name")),
        Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
        Price = r.GetDecimal(r.GetOrdinal("Price")),
        Stock = r.GetInt32(r.GetOrdinal("Stock")),
        CategoryId = r.GetInt32(r.GetOrdinal("CategoryId")),
        ImageUrl = r.IsDBNull(r.GetOrdinal("ImageUrl")) ? null : r.GetString(r.GetOrdinal("ImageUrl"))
    };
}