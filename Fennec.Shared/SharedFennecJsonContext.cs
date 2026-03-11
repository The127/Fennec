using System.Text.Json;
using Fennec.Shared.Dtos.Auth;
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
public partial class SharedFennecJsonContext : JsonSerializerContext
{
}