using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Auth;

public class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(string userId) => UserId = userId;

    public string UserId { get; }
}
