using Fennec.Client;

namespace Fennec.App.Messages;

public record HubConnectionStateChangedMessage(ConnectionStatus Status);
