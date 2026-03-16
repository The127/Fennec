using Fennec.App.Messages;

namespace Fennec.App.Services;

public interface ICursorPositionService
{
    void Start(CaptureTarget target);
    void Stop();
    event Action<float, float, CursorType, bool>? OnCursorChanged;
}
