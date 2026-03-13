using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fennec.App.Services;
using PortAudioSharp;
using Stream = PortAudioSharp.Stream;

namespace Fennec.App.ViewModels.Settings;

public record AudioDeviceItem(string Name, int DeviceIndex, bool IsDefault);
public record AudioHostApiItem(string Name, int Index);

public partial class AudioSettingsViewModel : ObservableObject, IDisposable
{
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private const int FrameSize = 960; // 20ms at 48kHz

    private readonly ISettingsStore _settingsStore;
    private readonly AppSettings _currentSettings;
    private bool _initialized;

    private Stream? _previewStream;
    private Stream.Callback? _previewCallback;
    private double _peakLevel;

    private Stream? _testToneStream;
    private Stream.Callback? _testToneCallback;
    private long _testToneSamplePos;
    private int _testToneRemainingSamples;
    private double _outputPeakLevel;

    public List<AudioHostApiItem> HostApis { get; } = [];
    public ObservableCollection<AudioDeviceItem> InputDevices { get; } = [];
    public ObservableCollection<AudioDeviceItem> OutputDevices { get; } = [];

    public bool ShowHostApiSelector => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && HostApis.Count > 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAlsaSelected))]
    [NotifyPropertyChangedFor(nameof(IsJackSelected))]
    private AudioHostApiItem? _selectedHostApi;

    [ObservableProperty]
    private AudioDeviceItem? _selectedInputDevice;

    [ObservableProperty]
    private AudioDeviceItem? _selectedOutputDevice;

    [ObservableProperty]
    private double _inputLevel;

    [ObservableProperty]
    private double _outputLevel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TestButtonLabel))]
    private bool _isTestingOutput;

    public string TestButtonLabel => IsTestingOutput ? "Stop" : "Test";

    public bool IsAlsaSelected => SelectedHostApi is { Name: "ALSA" };
    public bool IsJackSelected => SelectedHostApi is { Name: "JACK" };

    public AudioSettingsViewModel(ISettingsStore settingsStore, AppSettings currentSettings)
    {
        _settingsStore = settingsStore;
        _currentSettings = currentSettings;

        try
        {
            PortAudioEndPoint.EnsurePortAudioInitialized();
            EnumerateHostApis();
        }
        catch
        {
            // PortAudio not available — leave lists empty
        }

        _initialized = true;
    }

    [RelayCommand]
    private void SelectHostApi(AudioHostApiItem api) => SelectedHostApi = api;

    private void EnumerateHostApis()
    {
        var seen = new Dictionary<int, string>();
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            seen.TryAdd(info.hostApi, HostApiDisplayName(info.hostApi));
        }

        foreach (var (index, name) in seen.OrderBy(kv => kv.Key))
            HostApis.Add(new AudioHostApiItem(name, index));

        var targetApi = _currentSettings.AudioHostApi ?? GetDefaultHostApi();
        SelectedHostApi = HostApis.FirstOrDefault(h => h.Index == targetApi) ?? HostApis.FirstOrDefault();
    }

    partial void OnSelectedHostApiChanged(AudioHostApiItem? value)
    {
        RefreshDeviceLists();
        if (_initialized) _ = SaveAsync();
    }

    private void RefreshDeviceLists()
    {
        _initialized = false;

        InputDevices.Clear();
        OutputDevices.Clear();

        var defaultInput = new AudioDeviceItem("System Default", -1, true);
        var defaultOutput = new AudioDeviceItem("System Default", -1, true);
        InputDevices.Add(defaultInput);
        OutputDevices.Add(defaultOutput);

        AudioDeviceItem? matchedInput = null;
        AudioDeviceItem? matchedOutput = null;
        var apiIndex = SelectedHostApi?.Index;

        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (apiIndex is not null && info.hostApi != apiIndex) continue;

            if (info.maxInputChannels > 0)
            {
                var item = new AudioDeviceItem(info.name, i, false);
                InputDevices.Add(item);
                if (info.name == _currentSettings.InputDeviceName)
                    matchedInput = item;
            }

            if (info.maxOutputChannels > 0)
            {
                var item = new AudioDeviceItem(info.name, i, false);
                OutputDevices.Add(item);
                if (info.name == _currentSettings.OutputDeviceName)
                    matchedOutput = item;
            }
        }

        SelectedInputDevice = matchedInput ?? defaultInput;
        SelectedOutputDevice = matchedOutput ?? defaultOutput;

        _initialized = true;
    }

    partial void OnSelectedInputDeviceChanged(AudioDeviceItem? value)
    {
        if (_initialized) _ = SaveAsync();
        _ = RestartPreviewCaptureAsync();
    }

    partial void OnSelectedOutputDeviceChanged(AudioDeviceItem? value)
    {
        if (_initialized) _ = SaveAsync();
        _ = StopTestToneAsync();
    }

    [RelayCommand]
    private async Task TestOutput()
    {
        if (IsTestingOutput)
        {
            await StopTestToneAsync();
            return;
        }

        var device = SelectedOutputDevice;
        if (device is null) return;

        int deviceIndex;
        if (device.IsDefault)
        {
            deviceIndex = PortAudio.DefaultOutputDevice;
            if (deviceIndex == PortAudio.NoDevice) return;
        }
        else
        {
            deviceIndex = device.DeviceIndex;
        }

        try
        {
            _testToneSamplePos = 0;
            _testToneRemainingSamples = SampleRate * 2; // 2 seconds
            _outputPeakLevel = 0;
            _testToneCallback = TestToneCallback;

            var di = deviceIndex;
            var cb = _testToneCallback;
            var openTask = Task.Run(() =>
            {
                var deviceInfo = PortAudio.GetDeviceInfo(di);
                if (deviceInfo.maxOutputChannels <= 0) return null;

                var outputParams = new StreamParameters
                {
                    device = di,
                    channelCount = Channels,
                    sampleFormat = SampleFormat.Int16,
                    suggestedLatency = deviceInfo.defaultLowOutputLatency,
                };

                var s = new Stream(
                    inParams: null,
                    outParams: outputParams,
                    sampleRate: SampleRate,
                    framesPerBuffer: (uint)FrameSize,
                    streamFlags: StreamFlags.ClipOff,
                    callback: cb,
                    userData: null!
                );
                s.Start();
                return s;
            });

            var completed = await Task.WhenAny(openTask, Task.Delay(3000));
            if (completed != openTask || !openTask.IsCompletedSuccessfully) return;

            var stream = openTask.Result;
            if (stream is null) return;

            _testToneStream = stream;
            IsTestingOutput = true;
        }
        catch
        {
            _testToneStream = null;
        }
    }

    private StreamCallbackResult TestToneCallback(
        IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        int count = (int)frameCount;
        bool finishing = false;

        for (int i = 0; i < count; i++)
        {
            short sample = 0;
            if (_testToneRemainingSamples > 0)
            {
                // 440 Hz sine wave at ~25% volume
                double t = (double)_testToneSamplePos / SampleRate;
                sample = (short)(Math.Sin(2 * Math.PI * 440 * t) * short.MaxValue * 0.25);
                _testToneSamplePos++;
                _testToneRemainingSamples--;
            }
            else
            {
                finishing = true;
            }

            Marshal.WriteInt16(output, i * sizeof(short), sample);
        }

        // Compute output level from what we just wrote
        long sumSquares = 0;
        for (int i = 0; i < count; i++)
        {
            short s = Marshal.ReadInt16(output, i * sizeof(short));
            sumSquares += (long)s * s;
        }
        double rms = Math.Sqrt((double)sumSquares / count) / short.MaxValue;
        _outputPeakLevel = rms > _outputPeakLevel ? rms : _outputPeakLevel * 0.85 + rms * 0.15;
        var level = _outputPeakLevel;

        if (finishing)
        {
            Dispatcher.UIThread.Post(() =>
            {
                OutputLevel = 0;
                IsTestingOutput = false;
            });
            return StreamCallbackResult.Complete;
        }

        Dispatcher.UIThread.Post(() => OutputLevel = level);
        return StreamCallbackResult.Continue;
    }

    private async Task StopTestToneAsync()
    {
        var stream = _testToneStream;
        _testToneStream = null;
        _testToneCallback = null;

        if (stream is not null)
        {
            await Task.Run(() =>
            {
                try { stream.Stop(); } catch { }
                try { stream.Close(); } catch { }
                stream.Dispose();
            });
        }
        OutputLevel = 0;
        IsTestingOutput = false;
    }

    private async Task RestartPreviewCaptureAsync()
    {
        await StopPreviewCaptureAsync();
        InputLevel = 0;

        var device = SelectedInputDevice;
        if (device is null) return;

        int deviceIndex;
        if (device.IsDefault)
        {
            deviceIndex = PortAudio.DefaultInputDevice;
            if (deviceIndex == PortAudio.NoDevice) return;
        }
        else
        {
            deviceIndex = device.DeviceIndex;
        }

        try
        {
            var di = deviceIndex;
            _previewCallback = PreviewCaptureCallback;
            var cb = _previewCallback;
            var openTask = Task.Run(() =>
            {
                var deviceInfo = PortAudio.GetDeviceInfo(di);
                if (deviceInfo.maxInputChannels <= 0) return null;

                var inputParams = new StreamParameters
                {
                    device = di,
                    channelCount = Channels,
                    sampleFormat = SampleFormat.Int16,
                    suggestedLatency = deviceInfo.defaultLowInputLatency,
                };

                var s = new Stream(
                    inParams: inputParams,
                    outParams: null,
                    sampleRate: SampleRate,
                    framesPerBuffer: (uint)FrameSize,
                    streamFlags: StreamFlags.ClipOff,
                    callback: cb,
                    userData: null!
                );
                s.Start();
                return s;
            });

            var completed = await Task.WhenAny(openTask, Task.Delay(3000));
            if (completed != openTask || !openTask.IsCompletedSuccessfully) return;

            var stream = openTask.Result;
            if (stream is null) return;

            _previewStream = stream;
        }
        catch
        {
            // Device unavailable — level stays at 0
            _previewStream = null;
        }
    }

    private StreamCallbackResult PreviewCaptureCallback(
        IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        long sumSquares = 0;
        int count = (int)frameCount;

        for (int i = 0; i < count; i++)
        {
            short sample = Marshal.ReadInt16(input, i * sizeof(short));
            sumSquares += (long)sample * sample;
        }

        double rms = Math.Sqrt((double)sumSquares / count) / short.MaxValue;
        // Smooth: fast attack, slow decay
        _peakLevel = rms > _peakLevel ? rms : _peakLevel * 0.85 + rms * 0.15;

        Dispatcher.UIThread.Post(() => InputLevel = _peakLevel);

        return StreamCallbackResult.Continue;
    }

    private async Task StopPreviewCaptureAsync()
    {
        var stream = _previewStream;
        _previewStream = null;
        _previewCallback = null;

        if (stream is not null)
        {
            await Task.Run(() =>
            {
                try { stream.Stop(); } catch { }
                try { stream.Close(); } catch { }
                stream.Dispose();
            });
        }
    }

    private async Task SaveAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        settings.AudioHostApi = SelectedHostApi?.Index;
        settings.InputDeviceName = SelectedInputDevice is { IsDefault: false } inp ? inp.Name : null;
        settings.OutputDeviceName = SelectedOutputDevice is { IsDefault: false } outp ? outp.Name : null;
        await _settingsStore.SaveAsync(settings);
    }

    private static int? GetDefaultHostApi()
    {
        var defaultInput = PortAudio.DefaultInputDevice;
        if (defaultInput != PortAudio.NoDevice)
            return PortAudio.GetDeviceInfo(defaultInput).hostApi;

        var defaultOutput = PortAudio.DefaultOutputDevice;
        if (defaultOutput != PortAudio.NoDevice)
            return PortAudio.GetDeviceInfo(defaultOutput).hostApi;

        return null;
    }

    private static string HostApiDisplayName(int hostApiIndex)
    {
        return hostApiIndex switch
        {
            0 => "ALSA",
            1 => "JACK",
            _ => $"Audio API {hostApiIndex}",
        };
    }

    public void Dispose()
    {
        var preview = _previewStream;
        _previewStream = null;
        _previewCallback = null;
        if (preview is not null)
        {
            try { preview.Stop(); } catch { }
            try { preview.Close(); } catch { }
            preview.Dispose();
        }

        var testTone = _testToneStream;
        _testToneStream = null;
        _testToneCallback = null;
        if (testTone is not null)
        {
            try { testTone.Stop(); } catch { }
            try { testTone.Close(); } catch { }
            testTone.Dispose();
        }
    }
}
