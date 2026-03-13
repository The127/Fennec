using Fennec.App.Services;
using PortAudioSharp;
using Xunit.Abstractions;

namespace Fennec.App.Tests.Services;

public class PortAudioDeviceLearningTests
{
    private readonly ITestOutputHelper _output;

    public PortAudioDeviceLearningTests(ITestOutputHelper output)
    {
        _output = output;
        PortAudioEndPoint.EnsurePortAudioInitialized();
    }

    [Fact]
    public void ListAllDevices()
    {
        var defaultInput = PortAudio.DefaultInputDevice;
        var defaultOutput = PortAudio.DefaultOutputDevice;

        _output.WriteLine($"Device count: {PortAudio.DeviceCount}");
        _output.WriteLine($"Default input device index: {defaultInput}");
        _output.WriteLine($"Default output device index: {defaultOutput}");
        _output.WriteLine("");

        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            var markers = new List<string>();
            if (i == defaultInput) markers.Add("DEFAULT INPUT");
            if (i == defaultOutput) markers.Add("DEFAULT OUTPUT");
            var markerStr = markers.Count > 0 ? $" *** {string.Join(", ", markers)} ***" : "";

            _output.WriteLine($"[{i}] hostApi={info.hostApi} in={info.maxInputChannels} out={info.maxOutputChannels} name=\"{info.name}\"{markerStr}");
        }
    }

    [Fact]
    public void ListFilteredDevices()
    {
        var defaultInput = PortAudio.DefaultInputDevice;
        var defaultOutput = PortAudio.DefaultOutputDevice;

        int? preferredApi = null;
        if (defaultInput != PortAudio.NoDevice)
            preferredApi = PortAudio.GetDeviceInfo(defaultInput).hostApi;
        else if (defaultOutput != PortAudio.NoDevice)
            preferredApi = PortAudio.GetDeviceInfo(defaultOutput).hostApi;

        _output.WriteLine($"Preferred hostApi: {preferredApi}");
        _output.WriteLine("");

        _output.WriteLine("=== INPUT DEVICES (filtered) ===");
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (preferredApi is not null && info.hostApi != preferredApi) continue;
            if (info.maxInputChannels <= 0) continue;
            _output.WriteLine($"[{i}] in={info.maxInputChannels} name=\"{info.name}\"");
        }

        _output.WriteLine("");
        _output.WriteLine("=== OUTPUT DEVICES (filtered) ===");
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (preferredApi is not null && info.hostApi != preferredApi) continue;
            if (info.maxOutputChannels <= 0) continue;
            _output.WriteLine($"[{i}] out={info.maxOutputChannels} name=\"{info.name}\"");
        }
    }
}
