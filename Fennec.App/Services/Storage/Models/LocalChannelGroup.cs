using System.ComponentModel.DataAnnotations;

namespace Fennec.App.Services.Storage.Models;

public class LocalChannelGroup
{
    [Key]
    public required Guid Id { get; set; }
    
    public required string Name { get; set; }
    
    public required Guid ServerId { get; set; }
    
    public int? SortOrder { get; set; }
    
    public List<LocalChannel> Channels { get; set; } = [];
}