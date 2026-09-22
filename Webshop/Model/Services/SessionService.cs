using System;
using Microsoft.AspNetCore.Http;
using Webshop.Model;

// GDPR-best practice: gem kun minimal brugerdata i sessionen (Id, UserName, Role).
// Aldrig gem hele User-objektet eller følsomme persondata i sessionen.
using UserRole = Webshop.Model.User.UserRole;

namespace Webshop.Model.Services;

public sealed class SessionService : ISessionService
{
    private const string KeyUserId = "Auth:UserId";
    private const string KeyUserName = "Auth:UserName";
    private const string KeyUserRole = "Auth:UserRole";

    public void SetUserInSession(HttpContext ctx, int userId, string username, UserRole role)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));
        if (ctx.Session is null) throw new InvalidOperationException("Session is not available on the provided HttpContext.");

        ctx.Session.SetInt32(KeyUserId, userId);
        ctx.Session.SetString(KeyUserName, username ?? string.Empty);
        ctx.Session.SetInt32(KeyUserRole, (int)role);
    }

    public (int userId, string username, UserRole role)? GetUserFromSession(HttpContext ctx)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));
        if (ctx.Session is null) return null;

        var id = ctx.Session.GetInt32(KeyUserId);
        if (!id.HasValue) return null;

        var username = ctx.Session.GetString(KeyUserName) ?? string.Empty;
        var roleInt = ctx.Session.GetInt32(KeyUserRole) ?? 0;

        return (id.Value, username, (UserRole)roleInt);
    }

    public void ClearSession(HttpContext ctx)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));
        if (ctx.Session is null) return;

        ctx.Session.Remove(KeyUserId);
        ctx.Session.Remove(KeyUserName);
        ctx.Session.Remove(KeyUserRole);
    }
}
