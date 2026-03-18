using System.ComponentModel.DataAnnotations;
using Fennec.App.Domain;

namespace Fennec.App.Services.Storage.Models;

public class LocalServer
{
    [Key]
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required InstanceUrl InstanceUrl { get; set; }
    
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    
    public int? SortOrder { get; set; }
    
    public List<LocalChannelGroup> ChannelGroups { get; set; } = [];
}