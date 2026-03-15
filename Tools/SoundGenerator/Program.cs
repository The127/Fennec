// One-time tool to generate WAV sound files for voice event feedback.
// Run: dotnet run --project Tools/SoundGenerator
// Output: Fennec.App/Assets/Sounds/{pack}/{effect}.wav

const int SampleRate = 48000;
const int BitsPerSample = 16;
const int Channels = 1;

var basePath = Path.Combine(FindRepoRoot(), "Fennec.App", "Assets", "Sounds");

GeneratePack(basePath, "Classic", classic: true);
GeneratePack(basePath, "Soft", soft: true);
GeneratePack(basePath, "Minimal", minimal: true);

Console.WriteLine("Done. Generated sound packs in " + basePath);

string FindRepoRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir, "Fennec.App")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    // fallback: assume run from repo root
    return Directory.GetCurrentDirectory();
}

void GeneratePack(string basePath, string packName, bool classic = false, bool soft = false, bool minimal = false)
{
    var dir = Path.Combine(basePath, packName);
    Directory.CreateDirectory(dir);

    var volume = soft ? 0.15 : (minimal ? 0.3 : 0.25);
    var fadeMs = minimal ? 5 : (soft ? 15 : 10);

    // Join: ascending tone(s)
    if (minimal)
        WriteWav(Path.Combine(dir, "join.wav"), GenerateTone(800, 60, volume, fadeMs));
    else if (soft)
        WriteWav(Path.Combine(dir, "join.wav"), Concat(
            GenerateTone(400, 100, volume, fadeMs),
            Silence(20),
            GenerateTone(600, 120, volume, fadeMs)));
    else // classic
        WriteWav(Path.Combine(dir, "join.wav"), Concat(
            GenerateTone(523, 80, volume, fadeMs),  // C5
            Silence(15),
            GenerateTone(659, 80, volume, fadeMs),  // E5
            Silence(15),
            GenerateTone(784, 100, volume, fadeMs))); // G5

    // Leave: descending tone(s)
    if (minimal)
        WriteWav(Path.Combine(dir, "leave.wav"), GenerateTone(500, 60, volume, fadeMs));
    else if (soft)
        WriteWav(Path.Combine(dir, "leave.wav"), Concat(
            GenerateTone(600, 120, volume, fadeMs),
            Silence(20),
            GenerateTone(400, 100, volume, fadeMs)));
    else
        WriteWav(Path.Combine(dir, "leave.wav"), Concat(
            GenerateTone(784, 80, volume, fadeMs),
            Silence(15),
            GenerateTone(659, 80, volume, fadeMs),
            Silence(15),
            GenerateTone(523, 100, volume, fadeMs)));

    // Mute: short low click/thud
    if (minimal)
        WriteWav(Path.Combine(dir, "mute.wav"), GenerateClick(300, 25, volume));
    else if (soft)
        WriteWav(Path.Combine(dir, "mute.wav"), GenerateTone(300, 60, volume, fadeMs));
    else
        WriteWav(Path.Combine(dir, "mute.wav"), GenerateTone(350, 50, volume, fadeMs));

    // Unmute: short high click/pop
    if (minimal)
        WriteWav(Path.Combine(dir, "unmute.wav"), GenerateClick(900, 25, volume));
    else if (soft)
        WriteWav(Path.Combine(dir, "unmute.wav"), GenerateTone(700, 60, volume, fadeMs));
    else
        WriteWav(Path.Combine(dir, "unmute.wav"), GenerateTone(800, 50, volume, fadeMs));

    // Deafen: two low tones
    if (minimal)
        WriteWav(Path.Combine(dir, "deafen.wav"), Concat(
            GenerateClick(300, 20, volume),
            Silence(15),
            GenerateClick(250, 20, volume)));
    else if (soft)
        WriteWav(Path.Combine(dir, "deafen.wav"), Concat(
            GenerateTone(350, 70, volume, fadeMs),
            Silence(20),
            GenerateTone(280, 70, volume, fadeMs)));
    else
        WriteWav(Path.Combine(dir, "deafen.wav"), Concat(
            GenerateTone(400, 60, volume, fadeMs),
            Silence(15),
            GenerateTone(300, 60, volume, fadeMs)));

    // Undeafen: two high tones
    if (minimal)
        WriteWav(Path.Combine(dir, "undeafen.wav"), Concat(
            GenerateClick(800, 20, volume),
            Silence(15),
            GenerateClick(1000, 20, volume)));
    else if (soft)
        WriteWav(Path.Combine(dir, "undeafen.wav"), Concat(
            GenerateTone(600, 70, volume, fadeMs),
            Silence(20),
            GenerateTone(750, 70, volume, fadeMs)));
    else
        WriteWav(Path.Combine(dir, "undeafen.wav"), Concat(
            GenerateTone(700, 60, volume, fadeMs),
            Silence(15),
            GenerateTone(900, 60, volume, fadeMs)));

    Console.WriteLine($"  Generated pack: {packName}");
}

short[] GenerateTone(double freqHz, int durationMs, double volume, int fadeMs)
{
    int numSamples = SampleRate * durationMs / 1000;
    int fadeSamples = SampleRate * fadeMs / 1000;
    var samples = new short[numSamples];

    for (int i = 0; i < numSamples; i++)
    {
        double t = (double)i / SampleRate;
        double envelope = 1.0;

        // Fade in
        if (i < fadeSamples)
            envelope = (double)i / fadeSamples;
        // Fade out
        else if (i > numSamples - fadeSamples)
            envelope = (double)(numSamples - i) / fadeSamples;

        double sample = Math.Sin(2 * Math.PI * freqHz * t) * volume * envelope;
        samples[i] = (short)(sample * short.MaxValue);
    }

    return samples;
}

short[] GenerateClick(double freqHz, int durationMs, double volume)
{
    int numSamples = SampleRate * durationMs / 1000;
    var samples = new short[numSamples];

    for (int i = 0; i < numSamples; i++)
    {
        double t = (double)i / SampleRate;
        // Exponential decay for click sound
        double envelope = Math.Exp(-8.0 * i / numSamples);
        double sample = Math.Sin(2 * Math.PI * freqHz * t) * volume * envelope;
        samples[i] = (short)(sample * short.MaxValue);
    }

    return samples;
}

short[] Silence(int durationMs)
{
    return new short[SampleRate * durationMs / 1000];
}

short[] Concat(params short[][] arrays)
{
    int total = arrays.Sum(a => a.Length);
    var result = new short[total];
    int offset = 0;
    foreach (var arr in arrays)
    {
        Array.Copy(arr, 0, result, offset, arr.Length);
        offset += arr.Length;
    }
    return result;
}

void WriteWav(string path, short[] samples)
{
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);

    int dataSize = samples.Length * (BitsPerSample / 8);
    int fileSize = 36 + dataSize;

    // RIFF header
    bw.Write("RIFF"u8);
    bw.Write(fileSize);
    bw.Write("WAVE"u8);

    // fmt chunk
    bw.Write("fmt "u8);
    bw.Write(16);               // chunk size
    bw.Write((short)1);         // PCM format
    bw.Write((short)Channels);
    bw.Write(SampleRate);
    bw.Write(SampleRate * Channels * BitsPerSample / 8); // byte rate
    bw.Write((short)(Channels * BitsPerSample / 8));     // block align
    bw.Write((short)BitsPerSample);

    // data chunk
    bw.Write("data"u8);
    bw.Write(dataSize);
    foreach (var sample in samples)
        bw.Write(sample);

    Console.WriteLine($"    {Path.GetFileName(path)} ({samples.Length} samples, {samples.Length * 1000 / SampleRate}ms)");
}
