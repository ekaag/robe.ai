using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Auth;

public class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(string userId, string? name = null, string? provider = null)
    {
        UserId   = userId;
        Name     = name;
        Provider = provider;
    }

    public string  UserId   { get; }
    public string? Name     { get; }
    public string? Provider { get; }
}
