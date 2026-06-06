namespace Robe.Core.Domain;

public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; set; }
    public string CreatedByUserId { get; init; } = "";
    public string ModifiedByUserId { get; set; } = "";
}
