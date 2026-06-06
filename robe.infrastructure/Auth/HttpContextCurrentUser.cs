using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Auth;

public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public string UserId =>
        _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("No authenticated user in the current HTTP context.");
}
