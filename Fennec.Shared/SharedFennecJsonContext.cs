using Fennec.Shared.Dtos.Auth;
using Fennec.Shared.Dtos.Federation;
using Fennec.Shared.Dtos.Server;
using Fennec.Shared.Dtos.User;

namespace Fennec.Shared;

using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(LoginRequestDto))]
[JsonSerializable(typeof(LoginResponseDto))]
[JsonSerializable(typeof(RegisterUserRequestDto))]
[JsonSerializable(typeof(GetPublicTokenRequestDto))]
[JsonSerializable(typeof(GetPublicTokenResponseDto))]
[JsonSerializable(typeof(CreateServerRequestDto))]
[JsonSerializable(typeof(CreateServerResponseDto))]
[JsonSerializable(typeof(JoinServerRequestDto))]
[JsonSerializable(typeof(MeResponseDto))]
[JsonSerializable(typeof(ListJoinedServersResponseDto))]
[JsonSerializable(typeof(CreateServerInviteRequestDto))]
[JsonSerializable(typeof(CreateServerInviteResponseDto))]
[JsonSerializable(typeof(ListServerInvitesResponseDto))]
[JsonSerializable(typeof(CreateChannelGroupRequestDto))]
[JsonSerializable(typeof(CreateChannelGroupResponseDto))]
[JsonSerializable(typeof(RenameChannelGroupRequestDto))]
[JsonSerializable(typeof(ListChannelGroupsResponseDto))]
[JsonSerializable(typeof(CreateChannelRequestDto))]
[JsonSerializable(typeof(CreateChannelResponseDto))]
[JsonSerializable(typeof(RenameChannelRequestDto))]
[JsonSerializable(typeof(UpdateChannelTypeRequestDto))]
[JsonSerializable(typeof(ListChannelsResponseDto))]
[JsonSerializable(typeof(SendMessageRequestDto))]
[JsonSerializable(typeof(SendMessageResponseDto))]
[JsonSerializable(typeof(ListMessagesResponseDto))]
[JsonSerializable(typeof(MessageReceivedDto))]
[JsonSerializable(typeof(ListServerMembersResponseDto))]
[JsonSerializable(typeof(ServerPresenceEntryDto))]
[JsonSerializable(typeof(List<ServerPresenceEntryDto>))]
[JsonSerializable(typeof(FederationPresencePushRequestDto))]
public partial class SharedFennecJsonContext : JsonSerializerContext
{
}