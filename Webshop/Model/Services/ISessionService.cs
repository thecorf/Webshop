using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Webshop.Model;

// Alias nested enum to simplify signatures
using UserRole = Webshop.Model.User.UserRole;

namespace Webshop.Model.Services;

public interface ISessionService
{
    void SetUserInSession(HttpContext ctx, int userId, string username, UserRole role);

    (int userId, string username, UserRole role)? GetUserFromSession(HttpContext ctx);

    void ClearSession(HttpContext ctx);
}
