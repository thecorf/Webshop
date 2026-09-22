using System.Threading;
using System.Threading;
using System.Threading.Tasks;
using Webshop.Model;

namespace Webshop.Model.Repos;

public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default);

    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<User> CreateUserAsync(User user, string password, CancellationToken ct = default);

    Task<bool> UpdatePasswordAsync(int userId, string newPassword, CancellationToken ct = default);

    Task UpdateAsync(User user, CancellationToken ct = default);
}
