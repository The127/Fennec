using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Fennec.Shared.Models;

namespace Fennec.App.Services.Storage.Models;

public class LocalServer
{
    [Key]
    public required Guid Id { get; set; }
    
    public required string Name { get; set; }
    
    public required string InstanceUrl { get; set; }
    
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    
    public int? SortOrder { get; set; }
    
    public List<LocalChannelGroup> ChannelGroups { get; set; } = [];
}

public class LocalChannelGroup
{
    [Key]
    public required Guid Id { get; set; }
    
    public required string Name { get; set; }
    
    public required Guid ServerId { get; set; }
    
    public int? SortOrder { get; set; }
    
    public List<LocalChannel> Channels { get; set; } = [];
}

public class LocalChannel
{
    [Key]
    public required Guid Id { get; set; }
    
    public required string Name { get; set; }
    
    public required Guid ChannelGroupId { get; set; }
    
    public required Guid ServerId { get; set; }
    
    public required ChannelType ChannelType { get; set; }
    
    public int? SortOrder { get; set; }
}

public class LocalUser
{
    [Key]
    public required string FederatedId { get; set; } // e.g. "alice@home.com" or "bob@instance.org"
    
    public required string Username { get; set; }
    
    public string? AvatarHash { get; set; }
    
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
