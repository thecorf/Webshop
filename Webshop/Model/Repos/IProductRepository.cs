using Webshop.Model;

namespace Webshop.Model.Repos;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Product>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<List<Product>> SearchAsync(string searchTerm, CancellationToken ct = default);
    Task<Product> CreateAsync(Product product, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<Dictionary<int, string>> GetCategoryMapAsync(CancellationToken ct = default);
}