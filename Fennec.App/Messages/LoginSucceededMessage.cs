using Fennec.App.Services.Auth;

namespace Fennec.App.Messages;

public record LoginSucceededMessage(AuthSession Session);