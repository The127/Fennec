using Fennec.Shared.Models;

namespace Fennec.App.Domain;

public record ChannelSummary(Guid Id, string Name, ChannelType ChannelType, Guid ChannelGroupId);
