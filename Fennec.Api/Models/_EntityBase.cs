using NodaTime;

namespace Fennec.Api.Models;

public class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Instant CreatedAt { get; set; }
    public Instant UpdatedAt { get; set; }
}