using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Webshop.Model;

namespace Webshop.Model.Repos;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<IEnumerable<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken);

    Task<IEnumerable<Product>> SearchAsync(string searchTerm, decimal? maxPrice, CancellationToken cancellationToken);
}
