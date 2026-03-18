namespace Fennec.App.Domain;

public abstract class OutgoingMessageState;

public sealed class PendingState : OutgoingMessageState;

public sealed class DeliveredState : OutgoingMessageState;

public sealed class FailedState(string reason) : OutgoingMessageState
{
    public string Reason { get; } = reason;
}
