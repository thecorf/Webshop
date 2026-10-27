using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Webshop.Model;

namespace Webshop.Model.Repos;

public interface IOrderRepository
{
    Task<int> CreateOrderAsync(Order order, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Order>> GetByUserIdAsync(int userId, CancellationToken ct = default);
}