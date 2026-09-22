using Microsoft.AspNetCore.Mvc.RazorPages;
using Webshop.Model.Repos;
using Webshop.Model.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Webshop.Model;

public abstract class BasePageModel : PageModel
{
    protected IUserRepository UserRepository { get; }
    protected ISessionService SessionService { get; }

    public BasePageModel(IUserRepository userRepository, ISessionService sessionService)
        => (UserRepository, SessionService) = (userRepository, sessionService);

    protected User? CurrentUser => SessionService.GetUserFromSession(HttpContext) is var data && data.HasValue
        ? UserRepository.GetUserByIdAsync(data.Value.userId).GetAwaiter().GetResult()
        : null;
}
