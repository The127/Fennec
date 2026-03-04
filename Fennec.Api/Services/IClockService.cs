using NodaTime;

namespace Fennec.Api.Services;

public interface IClockService
{
    Instant GetCurrentInstant();
}