namespace Robe.Core.Interfaces;

public interface ICurrentUser
{
    string UserId { get; }
    string? Name { get; }
    string? Provider { get; }
}
