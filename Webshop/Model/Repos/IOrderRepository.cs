using System.Threading;
using System.Threading;
using System.Threading.Tasks;
using Webshop.Model;

namespace Webshop.Model.Repos;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);
    // Add order-related methods as needed
}
