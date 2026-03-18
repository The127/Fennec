using System.ComponentModel.DataAnnotations;
using Fennec.App.Domain;
using Fennec.Shared.Models;

namespace Fennec.App.Services.Storage.Models;

public class LocalChannel
{
    [Key]
    public required Guid Id { get; set; }

    public required string Name { get; set; }

    public required Guid ChannelGroupId { get; set; }

    public required Guid ServerId { get; set; }

    public required ChannelType ChannelType { get; set; }

    public int? SortOrder { get; set; }

    public void UpdateFrom(ChannelSummary summary, int sortOrder)
    {
        Name = summary.Name;
        ChannelType = summary.ChannelType;
        SortOrder = sortOrder;
    }
}