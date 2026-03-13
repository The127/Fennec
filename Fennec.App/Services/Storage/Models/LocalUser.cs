using System.ComponentModel.DataAnnotations;

namespace Fennec.App.Services.Storage.Models;

public class LocalUser
{
    [Key]
    public required string FederatedId { get; set; } // e.g. "alice@home.com" or "bob@instance.org"
    
    public required string Username { get; set; }
    
    public string? AvatarHash { get; set; }
    
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}