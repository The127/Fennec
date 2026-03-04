using NodaTime;

namespace Fennec.Api.Services;

public class ServerClockService : IClockService
{
    public Instant GetCurrentInstant()
    {
        return SystemClock.Instance.GetCurrentInstant();
    }
}