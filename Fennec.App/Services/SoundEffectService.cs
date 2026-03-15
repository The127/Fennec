using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using PortAudioSharp;
using Stream = PortAudioSharp.Stream;

namespace Fennec.App.Services;

public enum SoundEffect
{
    Join,
    Leave,
    Mute,
    Unmute,
    Deafen,
    Undeafen,
}

public interface ISoundEffectService
{
    Task PlayAsync(SoundEffect effect);
}

public class SoundEffectService : ISoundEffectService
{
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private const int FrameSize = 960;

    private readonly ISettingsStore _settingsStore;
    private readonly ILogger<SoundEffectService> _logger;

    public SoundEffectService(ISettingsStore settingsStore, ILogger<SoundEffectService> logger)
    {
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public async Task PlayAsync(SoundEffect effect)
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            if (!settings.VoiceSoundsEnabled)
                return;

            var samples = LoadWavSamples(settings.VoiceSoundPack, effect);
            if (samples is null || samples.Length == 0)
                return;

            await PlaySamplesAsync(samples, settings);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to play sound effect {Effect}", effect);
        }
    }

    private static short[]? LoadWavSamples(string pack, SoundEffect effect)
    {
        var effectName = effect switch
        {
            SoundEffect.Join => "join",
            SoundEffect.Leave => "leave",
            SoundEffect.Mute => "mute",
            SoundEffect.Unmute => "unmute",
            SoundEffect.Deafen => "deafen",
            SoundEffect.Undeafen => "undeafen",
            _ => null,
        };

        if (effectName is null) return null;

        var uri = new Uri($"avares://Fennec.App/Assets/Sounds/{pack}/{effectName}.wav");

        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(uri);
            using var reader = new BinaryReader(stream);

            // Parse WAV header
            reader.ReadBytes(4); // "RIFF"
            reader.ReadInt32();  // file size
            reader.ReadBytes(4); // "WAVE"
            reader.ReadBytes(4); // "fmt "
            var fmtSize = reader.ReadInt32();
            reader.ReadBytes(fmtSize); // skip fmt chunk
            reader.ReadBytes(4); // "data"
            var dataSize = reader.ReadInt32();

            var sampleCount = dataSize / sizeof(short);
            var samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = reader.ReadInt16();

            return samples;
        }
        catch
        {
            return null;
        }
    }

    private async Task PlaySamplesAsync(short[] samples, AppSettings settings)
    {
        PortAudioEndPoint.EnsurePortAudioInitialized();

        var outputIndex = PortAudioEndPoint.FindDeviceByName(settings.OutputDeviceName, settings.AudioHostApi);
        int deviceIndex;
        if (outputIndex is not null)
        {
            deviceIndex = outputIndex.Value;
        }
        else
        {
            deviceIndex = PortAudio.DefaultOutputDevice;
            if (deviceIndex == PortAudio.NoDevice)
                return;
        }

        int position = 0;
        var samplesCopy = samples;

        Stream.Callback callback = (IntPtr input, IntPtr output, uint frameCount,
            ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData) =>
        {
            int count = (int)frameCount;
            for (int i = 0; i < count; i++)
            {
                short sample = position < samplesCopy.Length ? samplesCopy[position++] : (short)0;
                Marshal.WriteInt16(output, i * sizeof(short), sample);
            }

            return position >= samplesCopy.Length
                ? StreamCallbackResult.Complete
                : StreamCallbackResult.Continue;
        };

        var di = deviceIndex;
        var cb = callback;

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
        if (completed != openTask || !openTask.IsCompletedSuccessfully)
            return;

        var stream = openTask.Result;
        if (stream is null) return;

        // Wait for playback to complete, then clean up
        var durationMs = (int)(samples.Length * 1000.0 / SampleRate) + 100;
        await Task.Delay(durationMs);

        try { stream.Stop(); } catch { }
        try { stream.Close(); } catch { }
        stream.Dispose();
    }
}
