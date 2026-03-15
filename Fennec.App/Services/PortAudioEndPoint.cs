using System.Net;
using System.Runtime.InteropServices;
using Concentus.Enums;
using Concentus.Structs;
using Microsoft.Extensions.Logging;
using PortAudioSharp;
using SIPSorceryMedia.Abstractions;
using Stream = PortAudioSharp.Stream;

namespace Fennec.App.Services;

/// <summary>
/// Audio endpoint using PortAudio for capture and playback, with Opus encode/decode.
/// Implements IAudioSource (microphone) and IAudioSink (speaker).
/// </summary>
public sealed class PortAudioEndPoint : IAudioSource, IAudioSink, IDisposable
{
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private const int FrameSizeMs = 20;
    private const int FrameSamples = SampleRate * FrameSizeMs / 1000; // 960
    private const int MaxEncodedBytes = 4000;

    private readonly ILogger _logger;
    private readonly OpusEncoder _encoder;
    private readonly OpusDecoder _decoder;
    private readonly AudioFormat _audioFormat;

    private Stream? _captureStream;
    private Stream? _playbackStream;
    private bool _isPaused;
    private bool _isClosed;

    // Accumulate captured PCM samples until we have a full frame
    private readonly short[] _captureBuffer = new short[FrameSamples];
    private int _captureBufferPos;

    // Keep callback delegates alive to prevent GC
    private Stream.Callback? _captureCallback;
    private Stream.Callback? _playbackCallback;

    // Playback ring buffer (lock-free single-producer single-consumer)
    private readonly short[] _playbackRing = new short[SampleRate * 2]; // ~2s buffer
    private int _ringWritePos;
    private int _ringReadPos;

    private static bool _paInitialized;
    private static readonly object InitLock = new();

    public event EncodedSampleDelegate? OnAudioSourceEncodedSample;
    public event Action<EncodedAudioFrame>? OnAudioSourceEncodedFrameReady;
    public event RawAudioSampleDelegate? OnAudioSourceRawSample;
    public event SourceErrorDelegate? OnAudioSourceError;
    public event SourceErrorDelegate? OnAudioSinkError;
    public event Action<double>? OnCaptureLevel;

    private readonly int? _inputDeviceIndex;
    private readonly int? _outputDeviceIndex;

    public PortAudioEndPoint(ILogger logger, int? inputDeviceIndex = null, int? outputDeviceIndex = null)
    {
        _logger = logger;
        _inputDeviceIndex = inputDeviceIndex;
        _outputDeviceIndex = outputDeviceIndex;
        _audioFormat = new AudioFormat(AudioCodecsEnum.OPUS, 111, SampleRate, Channels, null);
        _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
        _decoder = new OpusDecoder(SampleRate, Channels);

        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (!_paInitialized)
            {
                PortAudio.LoadNativeLibrary();
                PortAudio.Initialize();
                _paInitialized = true;
                _logger.LogInformation("PortAudio initialized: {Version}", PortAudio.VersionInfo.ToString());
            }
        }
    }

    public Task StartAudio()
    {
        if (_captureStream is not null) return Task.CompletedTask;

        var inputDevice = _inputDeviceIndex ?? PortAudio.DefaultInputDevice;
        if (inputDevice == PortAudio.NoDevice)
        {
            _logger.LogWarning("No default input audio device found");
            return Task.CompletedTask;
        }

        var inputInfo = PortAudio.GetDeviceInfo(inputDevice);
        var inputParams = new StreamParameters
        {
            device = inputDevice,
            channelCount = Channels,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = inputInfo.defaultLowInputLatency,
        };

        _captureCallback = CaptureCallback;
        _captureStream = new Stream(
            inParams: inputParams,
            outParams: null,
            sampleRate: SampleRate,
            framesPerBuffer: (uint)FrameSamples,
            streamFlags: StreamFlags.ClipOff,
            callback: _captureCallback,
            userData: null!
        );

        _captureStream.Start();
        _logger.LogInformation("PortAudio capture started (device: {Device})", inputInfo.name);

        return Task.CompletedTask;
    }

    public Task StartAudioSink()
    {
        if (_playbackStream is not null) return Task.CompletedTask;

        var outputDevice = _outputDeviceIndex ?? PortAudio.DefaultOutputDevice;
        if (outputDevice == PortAudio.NoDevice)
        {
            _logger.LogWarning("No default output audio device found");
            return Task.CompletedTask;
        }

        var outputInfo = PortAudio.GetDeviceInfo(outputDevice);
        var outputParams = new StreamParameters
        {
            device = outputDevice,
            channelCount = Channels,
            sampleFormat = SampleFormat.Int16,
            suggestedLatency = outputInfo.defaultLowOutputLatency,
        };

        _playbackCallback = PlaybackCallback;
        _playbackStream = new Stream(
            inParams: null,
            outParams: outputParams,
            sampleRate: SampleRate,
            framesPerBuffer: (uint)FrameSamples,
            streamFlags: StreamFlags.ClipOff,
            callback: _playbackCallback,
            userData: null!
        );

        _playbackStream.Start();
        _logger.LogInformation("PortAudio playback started (device: {Device})", outputInfo.name);

        return Task.CompletedTask;
    }

    private StreamCallbackResult CaptureCallback(
        IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if (_isPaused || _isClosed) return StreamCallbackResult.Continue;

        int sampleCount = (int)frameCount;
        for (int i = 0; i < sampleCount; i++)
        {
            _captureBuffer[_captureBufferPos++] = Marshal.ReadInt16(input, i * sizeof(short));

            if (_captureBufferPos >= FrameSamples)
            {
                _captureBufferPos = 0;
                EncodeAndSend();
            }
        }

        return StreamCallbackResult.Continue;
    }

    private StreamCallbackResult PlaybackCallback(
        IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        int samples = (int)frameCount;
        int available = (_ringWritePos - _ringReadPos + _playbackRing.Length) % _playbackRing.Length;

        for (int i = 0; i < samples; i++)
        {
            short sample = 0;
            if (i < available)
            {
                sample = _playbackRing[_ringReadPos];
                _ringReadPos = (_ringReadPos + 1) % _playbackRing.Length;
            }
            Marshal.WriteInt16(output, i * sizeof(short), sample);
        }

        return StreamCallbackResult.Continue;
    }

    private void EncodeAndSend()
    {
        try
        {
            // Compute RMS for VAD
            long sumSquares = 0;
            for (int i = 0; i < FrameSamples; i++)
            {
                long s = _captureBuffer[i];
                sumSquares += s * s;
            }
            double rms = Math.Sqrt((double)sumSquares / FrameSamples) / short.MaxValue;
            OnCaptureLevel?.Invoke(rms);

            var encoded = new byte[MaxEncodedBytes];
            int encodedLength = _encoder.Encode(_captureBuffer, 0, FrameSamples, encoded, 0, MaxEncodedBytes);

            if (encodedLength > 0)
            {
                var trimmed = new byte[encodedLength];
                Buffer.BlockCopy(encoded, 0, trimmed, 0, encodedLength);

                uint durationRtpUnits = (uint)FrameSamples;
                OnAudioSourceEncodedSample?.Invoke(durationRtpUnits, trimmed);
            }
        }
        catch (Exception ex)
        {
            OnAudioSourceError?.Invoke(ex.Message);
        }
    }

    public void GotAudioRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp,
        int payloadID, bool marker, byte[] payload)
    {
        if (_isClosed || _playbackStream is null) return;

        try
        {
            var pcm = new short[FrameSamples];
            int decoded = _decoder.Decode(payload, 0, payload.Length, pcm, 0, FrameSamples, false);

            if (decoded > 0)
            {
                EnqueuePlayback(pcm, decoded);
            }
        }
        catch (Exception ex)
        {
            OnAudioSinkError?.Invoke(ex.Message);
        }
    }

    public void GotEncodedMediaFrame(EncodedAudioFrame encodedMediaFrame)
    {
        if (_isClosed || _playbackStream is null) return;

        try
        {
            var pcm = new short[FrameSamples];
            int decoded = _decoder.Decode(encodedMediaFrame.EncodedAudio, 0,
                encodedMediaFrame.EncodedAudio.Length, pcm, 0, FrameSamples, false);

            if (decoded > 0)
            {
                EnqueuePlayback(pcm, decoded);
            }
        }
        catch (Exception ex)
        {
            OnAudioSinkError?.Invoke(ex.Message);
        }
    }

    private void EnqueuePlayback(short[] pcm, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            int nextWrite = (_ringWritePos + 1) % _playbackRing.Length;
            if (nextWrite == _ringReadPos)
                break; // ring full, drop samples
            _playbackRing[_ringWritePos] = pcm[i];
            _ringWritePos = nextWrite;
        }
    }

    public Task PauseAudio()
    {
        _isPaused = true;
        return Task.CompletedTask;
    }

    public Task ResumeAudio()
    {
        _isPaused = false;
        return Task.CompletedTask;
    }

    public Task PauseAudioSink()
    {
        // Playback callback will output silence when ring is empty
        return Task.CompletedTask;
    }

    public Task ResumeAudioSink()
    {
        return Task.CompletedTask;
    }

    public Task CloseAudio()
    {
        if (_captureStream is not null)
        {
            try { _captureStream.Stop(); } catch { /* ignore */ }
            try { _captureStream.Close(); } catch { /* ignore */ }
            _captureStream.Dispose();
            _captureStream = null;
        }
        return Task.CompletedTask;
    }

    public Task CloseAudioSink()
    {
        if (_playbackStream is not null)
        {
            try { _playbackStream.Stop(); } catch { /* ignore */ }
            try { _playbackStream.Close(); } catch { /* ignore */ }
            _playbackStream.Dispose();
            _playbackStream = null;
        }
        return Task.CompletedTask;
    }

    public List<AudioFormat> GetAudioSourceFormats() => [_audioFormat];
    public void SetAudioSourceFormat(AudioFormat audioFormat) { }
    public void RestrictFormats(Func<AudioFormat, bool> filter) { }
    public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample) { }
    public bool HasEncodedAudioSubscribers() => OnAudioSourceEncodedSample != null;
    public bool IsAudioSourcePaused() => _isPaused;

    public List<AudioFormat> GetAudioSinkFormats() => [_audioFormat];
    public void SetAudioSinkFormat(AudioFormat audioFormat) { }

    /// <summary>
    /// Ensures PortAudio is initialized. Safe to call multiple times.
    /// </summary>
    public static void EnsurePortAudioInitialized()
    {
        lock (InitLock)
        {
            if (!_paInitialized)
            {
                PortAudio.LoadNativeLibrary();
                PortAudio.Initialize();
                _paInitialized = true;
            }
        }
    }

    /// <summary>
    /// Finds a device index by name, optionally scoped to a host API. Returns null if not found.
    /// </summary>
    public static int? FindDeviceByName(string? name, int? hostApi = null)
    {
        if (string.IsNullOrEmpty(name)) return null;

        EnsurePortAudioInitialized();

        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.name == name && (hostApi is null || info.hostApi == hostApi))
                return i;
        }

        return null;
    }

    public void Dispose()
    {
        _isClosed = true;
        _ = CloseAudio();
        _ = CloseAudioSink();
    }
}
