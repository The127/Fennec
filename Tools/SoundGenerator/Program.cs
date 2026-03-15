// One-time tool to generate WAV sound files for voice event feedback.
// Run: dotnet run --project Tools/SoundGenerator
// Output: Fennec.App/Assets/Sounds/{pack}/{effect}.wav
//
// Synthesis techniques used:
//   - Additive harmonic synthesis (fundamental + overtones with slight inharmonicity)
//   - Exponential ADSR envelopes
//   - Phase-continuous arpeggio with logarithmic portamento (glide)
//   - Logarithmic frequency chirps for transient sounds
//   - Noise transient at attack for physical "pluck" character
//   - Schroeder reverb (parallel comb filters → serial all-pass filters)
//   - Tanh soft clipping + peak normalization

const int SampleRate    = 48000;
const int BitsPerSample = 16;
const int Channels      = 1;

var rng      = new Random(42); // seeded for reproducibility
var basePath = Path.Combine(FindRepoRoot(), "Fennec.App", "Assets", "Sounds");

GeneratePack(basePath, "Classic");
GeneratePack(basePath, "Soft");
GeneratePack(basePath, "Minimal");

Console.WriteLine("Done. Generated sound packs in " + basePath);

// ─── Pack generation ──────────────────────────────────────────────────────────

string FindRepoRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir, "Fennec.App")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return Directory.GetCurrentDirectory();
}

void GeneratePack(string basePath, string packName)
{
    var dir = Path.Combine(basePath, packName);
    Directory.CreateDirectory(dir);

    bool isMinimal = packName == "Minimal";
    bool isSoft    = packName == "Soft";

    // Harmonic amplitude profiles (relative weights for harmonics 1, 2, 3, 4, 5)
    // Richer profiles → warmer, more instrument-like tone
    double[] richHarmonics = [1.0, 0.50, 0.25, 0.12, 0.06];
    double[] medHarmonics  = [1.0, 0.40, 0.18, 0.08];
    double[] thinHarmonics = [1.0, 0.25];

    double[] bellHarmonics = isMinimal ? thinHarmonics : (isSoft ? medHarmonics : richHarmonics);
    double reverbWet  = isMinimal ? 0.00 : (isSoft ? 0.22 : 0.16);
    double reverbRoom = isSoft ? 0.82 : 0.76;
    double transAmt   = isMinimal ? 0.10 : (isSoft ? 0.08 : 0.13);

    // ── Join: ascending C5→E5→G5 major arpeggio (bright, welcoming) ──────────
    double[] joinFreqs = isMinimal ? [800.0]                 : [523.25, 659.25, 783.99]; // C5, E5, G5
    int[]    joinDurs  = isMinimal ? [80]                    : (isSoft ? [110, 110, 140] : [95, 90, 120]);
    int joinAtk   = isMinimal ? 5  : (isSoft ? 12 : 8);
    int joinDec   = isMinimal ? 15 : (isSoft ? 28 : 20);
    int joinRel   = isMinimal ? 20 : (isSoft ? 45 : 30);
    int joinGlide = isMinimal ? 0  : (isSoft ? 22 : 14);
    double joinSus = 0.68;

    var join = isMinimal
        ? Reverb(AddTransient(Note(joinFreqs[0], joinDurs[0], joinAtk, joinDec, joinSus, joinRel, bellHarmonics), transAmt), reverbWet, reverbRoom)
        : Reverb(AddTransient(Arpeggio(joinFreqs, joinDurs, joinGlide, joinAtk, joinDec, joinSus, joinRel, bellHarmonics), transAmt), reverbWet, reverbRoom);
    WriteWav(Path.Combine(dir, "join.wav"), join);

    // ── Leave: descending G5→E5→C5 (neutral, understated) ───────────────────
    double[] leaveFreqs = isMinimal ? [500.0]                : [783.99, 659.25, 523.25];
    int[]    leaveDurs  = isMinimal ? [80]                   : (isSoft ? [130, 110, 110] : [110, 90, 90]);
    int leaveRel = isMinimal ? 22 : (isSoft ? 50 : 35);

    var leave = isMinimal
        ? Reverb(AddTransient(Note(leaveFreqs[0], leaveDurs[0], joinAtk, joinDec, joinSus * 0.9, leaveRel, bellHarmonics), transAmt * 0.8), reverbWet, reverbRoom)
        : Reverb(AddTransient(Arpeggio(leaveFreqs, leaveDurs, joinGlide, joinAtk, joinDec, joinSus * 0.88, leaveRel, bellHarmonics), transAmt * 0.8), reverbWet, reverbRoom);
    WriteWav(Path.Combine(dir, "leave.wav"), Scale(leave, 0.88));

    // ── Mute: sharp downward chirp (mic closing, decisive) ───────────────────
    double muteHi  = isMinimal ? 680 : (isSoft ? 660 : 720);
    double muteLo  = isMinimal ? 320 : (isSoft ? 280 : 260);
    int    muteDur = isMinimal ? 65  : (isSoft ? 105 : 95);
    int    muteAtk = isMinimal ? 3   : (isSoft ? 5 : 4);
    int    muteRel = isMinimal ? 25  : (isSoft ? 45 : 38);

    var mute = Reverb(AddTransient(Chirp(muteHi, muteLo, muteDur, muteAtk, muteRel, medHarmonics), transAmt * 0.5), reverbWet * 0.4, reverbRoom);
    WriteWav(Path.Combine(dir, "mute.wav"), mute);

    // ── Unmute: sharp upward chirp (mic opening, bright) ─────────────────────
    double unmuteHi  = isMinimal ? 720 : (isSoft ? 700 : 780);
    double unmuteLo  = isMinimal ? 320 : (isSoft ? 280 : 260);
    int    unmuteDur = isMinimal ? 65  : (isSoft ? 105 : 95);

    var unmute = Reverb(AddTransient(Chirp(unmuteLo, unmuteHi, unmuteDur, muteAtk, muteRel, medHarmonics), transAmt * 0.5), reverbWet * 0.4, reverbRoom);
    WriteWav(Path.Combine(dir, "unmute.wav"), unmute);

    // ── Deafen: two descending chirps, lower register (ears covered) ──────────
    double def1Hi = isMinimal ? 520 : (isSoft ? 500 : 560);
    double def1Lo = isMinimal ? 180 : (isSoft ? 160 : 160);
    int    def1D  = isMinimal ? 55  : (isSoft ? 88 : 82);
    int    defGap = isMinimal ? 22  : (isSoft ? 32 : 27);
    double def2Hi = isMinimal ? 460 : (isSoft ? 440 : 490);
    double def2Lo = isMinimal ? 140 : (isSoft ? 120 : 130);
    int    def2D  = isMinimal ? 50  : (isSoft ? 78 : 72);

    var deafen = Reverb(
        Concat(
            AddTransient(Chirp(def1Hi, def1Lo, def1D, muteAtk, muteRel, richHarmonics), transAmt * 0.6),
            Silence(defGap),
            AddTransient(Chirp(def2Hi, def2Lo, def2D, muteAtk, muteRel, richHarmonics), transAmt * 0.5)),
        reverbWet * 0.55, reverbRoom);
    WriteWav(Path.Combine(dir, "deafen.wav"), Scale(deafen, 1.05));

    // ── Undeafen: two ascending chirps, higher register (ears open) ───────────
    double und1Lo = isMinimal ? 200 : (isSoft ? 200 : 195);
    double und1Hi = isMinimal ? 600 : (isSoft ? 580 : 660);
    int    und1D  = isMinimal ? 55  : (isSoft ? 82 : 77);
    int    undGap = isMinimal ? 20  : (isSoft ? 28 : 23);
    double und2Lo = isMinimal ? 230 : (isSoft ? 235 : 230);
    double und2Hi = isMinimal ? 730 : (isSoft ? 710 : 800);
    int    und2D  = isMinimal ? 60  : (isSoft ? 92 : 87);

    var undeafen = Reverb(
        Concat(
            AddTransient(Chirp(und1Lo, und1Hi, und1D, muteAtk, muteRel, medHarmonics), transAmt * 0.5),
            Silence(undGap),
            AddTransient(Chirp(und2Lo, und2Hi, und2D, muteAtk, muteRel, medHarmonics), transAmt * 0.6)),
        reverbWet * 0.55, reverbRoom);
    WriteWav(Path.Combine(dir, "undeafen.wav"), Scale(undeafen, 1.05));

    Console.WriteLine($"  Generated pack: {packName}");
}

// ─── Core synthesis functions (all return double[], amplitude roughly -1..1) ──

// Single held note with additive harmonic synthesis and exponential ADSR.
// 'harmonics' = relative amplitude of each partial (index 0 = fundamental).
double[] Note(double freqHz, int durationMs,
              int attackMs, int decayMs, double sustainLevel, int releaseMs,
              double[] harmonics)
{
    int n  = Ms(durationMs);
    int na = Ms(attackMs);
    int nd = Ms(decayMs);
    int nr = Ms(releaseMs);
    int ns = Math.Max(0, n - na - nd - nr);
    var buf = new double[n];
    double totalAmp = harmonics.Sum();

    for (int i = 0; i < n; i++)
    {
        double t = (double)i / SampleRate;

        // Additive synthesis with slight inharmonicity for acoustic warmth
        double wave = 0;
        for (int h = 0; h < harmonics.Length; h++)
        {
            double freq = freqHz * (h + 1) * (1 + h * h * 0.0002);
            wave += Math.Sin(2 * Math.PI * freq * t) * harmonics[h];
        }
        wave /= totalAmp;

        buf[i] = wave * Adsr(i, na, nd, ns, nr, sustainLevel);
    }
    return buf;
}

// Frequency sweep (chirp) with phase accumulation for smooth glide.
// Logarithmic sweep: sounds natural because pitch perception is logarithmic.
double[] Chirp(double startHz, double endHz, int durationMs,
               int attackMs, int releaseMs, double[] harmonics)
{
    int n  = Ms(durationMs);
    int na = Ms(attackMs);
    int nr = Ms(releaseMs);
    var buf = new double[n];
    var ph  = new double[harmonics.Length]; // per-harmonic phase accumulators
    double totalAmp = harmonics.Sum();

    for (int i = 0; i < n; i++)
    {
        double x    = (double)i / n;
        double freq = startHz * Math.Pow(endHz / startHz, x); // logarithmic sweep

        // Envelope: linear attack | exponential decay during sweep | exponential release
        double env;
        if (i < na)
        {
            env = (double)i / na;
        }
        else if (i > n - nr)
        {
            double rx = (double)(i - (n - nr)) / nr;
            env = Math.Exp(-4.0 * rx);
        }
        else
        {
            double sx = (double)(i - na) / Math.Max(1, n - na - nr);
            env = Math.Exp(-2.0 * sx); // natural exponential decay
        }

        // Phase accumulation ensures clean, continuous frequency sweep
        for (int h = 0; h < harmonics.Length; h++)
            ph[h] += 2 * Math.PI * freq * (h + 1) / SampleRate;

        double wave = 0;
        for (int h = 0; h < harmonics.Length; h++)
            wave += Math.Sin(ph[h]) * harmonics[h];
        wave /= totalAmp;

        buf[i] = wave * env;
    }
    return buf;
}

// Multi-note arpeggio with portamento (logarithmic pitch glide between notes).
// Phase is continuous across note boundaries — no clicks or discontinuities.
double[] Arpeggio(double[] freqs, int[] durationsMsPerNote, int glideMs,
                  int attackMs, int decayMs, double sustainLevel, int releaseMs,
                  double[] harmonics)
{
    int total = durationsMsPerNote.Select(Ms).Sum();
    var buf   = new double[total];
    var ph    = new double[harmonics.Length]; // phases carry across notes
    double totalAmp = harmonics.Sum();
    int pos = 0;

    for (int ni = 0; ni < freqs.Length; ni++)
    {
        double targetHz = freqs[ni];
        double prevHz   = ni == 0 ? targetHz : freqs[ni - 1];
        int ns  = Ms(durationsMsPerNote[ni]);
        int ng  = ni == 0 ? 0 : Math.Min(Ms(glideMs), ns / 3); // no glide on first note
        int na  = Ms(attackMs);
        int nd  = Ms(decayMs);
        int nr  = Ms(releaseMs);
        int nsu = Math.Max(0, ns - (ni == 0 ? na : na / 3) - nd - nr);

        for (int i = 0; i < ns; i++)
        {
            // Logarithmic portamento: pitch glides smoothly from previous note
            double freq = (ng > 0 && i < ng)
                ? prevHz * Math.Pow(targetHz / prevHz, (double)i / ng)
                : targetHz;

            // First note: full attack envelope. Subsequent notes: brief re-attack
            // from sustain level (so arpeggio has articulation without clicking).
            double env;
            int noteAtk = ni == 0 ? na : na / 3;
            if (i < noteAtk)
            {
                env = ni == 0
                    ? Math.Pow((double)i / noteAtk, 1.5)                             // smooth exponential attack
                    : sustainLevel + (1.0 - sustainLevel) * (double)i / noteAtk;    // brief articulation
            }
            else if (i < noteAtk + nd)
            {
                double dx = (double)(i - noteAtk) / nd;
                env = 1.0 - (1.0 - sustainLevel) * (1.0 - Math.Exp(-4.0 * dx));    // exponential decay
            }
            else if (i < ns - nr)
            {
                env = sustainLevel;
            }
            else
            {
                double rx = (double)(i - (ns - nr)) / nr;
                env = sustainLevel * Math.Exp(-5.0 * rx);                           // exponential release
            }

            // Phase accumulation (continuous across notes → no clicks at boundaries)
            for (int h = 0; h < harmonics.Length; h++)
                ph[h] += 2 * Math.PI * freq * (h + 1) / SampleRate;

            double wave = 0;
            for (int h = 0; h < harmonics.Length; h++)
                wave += Math.Sin(ph[h]) * harmonics[h];
            wave /= totalAmp;

            buf[pos + i] = wave * env;
        }
        pos += ns;
    }
    return buf;
}

// Add a brief band-pass noise burst at the attack for physical "pluck" character.
// This is what makes synthesized tones feel like real struck/plucked instruments.
double[] AddTransient(double[] samples, double strength)
{
    int dur = Math.Min(Ms(8), samples.Length); // 8ms transient
    var result = (double[])samples.Clone();
    double prev = 0;

    for (int i = 0; i < dur; i++)
    {
        double noise    = (rng.NextDouble() * 2 - 1) * strength * Math.Exp(-12.0 * i / dur);
        double filtered = 0.4 * noise + 0.6 * prev; // simple 1-pole LP for band shaping
        prev = filtered;
        result[i] += filtered;
    }
    return result;
}

// Schroeder reverb: 4 parallel comb filters → 2 serial all-pass filters.
// Adds room depth and warmth. Delays scaled to 48 kHz from Freeverb values.
double[] Reverb(double[] input, double wetMix, double roomSize)
{
    if (wetMix <= 0) return input;

    // Comb filter delays (samples at 48kHz) and feedback
    int[] combDelays = [1217, 1294, 1478, 1556];
    double feedback  = 0.74 + roomSize * 0.12; // ~0.83-0.87 depending on room

    // All-pass delays
    int[] apDelays = [245, 606];
    double apGain  = 0.7;

    int outLen = input.Length + Ms(250); // 250ms reverb tail
    var wet    = new double[outLen];

    // 4 parallel comb filters (summed)
    foreach (int d in combDelays)
    {
        var cbuf = new double[d];
        int cp   = 0;
        for (int i = 0; i < outLen; i++)
        {
            double x       = i < input.Length ? input[i] : 0.0;
            double delayed = cbuf[cp];
            cbuf[cp] = x + delayed * feedback;
            cp       = (cp + 1) % d;
            wet[i]  += delayed * 0.25; // equal mix of 4 combs
        }
    }

    // 2 serial all-pass filters (smooth out comb coloration)
    foreach (int d in apDelays)
    {
        var abuf = new double[d];
        int ap   = 0;
        var next = new double[outLen];
        for (int i = 0; i < outLen; i++)
        {
            double x       = wet[i];
            double delayed = abuf[ap];
            double y       = -apGain * x + delayed;
            abuf[ap] = x + apGain * delayed;
            ap       = (ap + 1) % d;
            next[i]  = y;
        }
        wet = next;
    }

    // Mix dry + wet
    var result = new double[outLen];
    for (int i = 0; i < outLen; i++)
    {
        double dry = i < input.Length ? input[i] : 0.0;
        result[i]  = dry * (1 - wetMix) + wet[i] * wetMix;
    }
    return result;
}

// ─── Utility functions ────────────────────────────────────────────────────────

int Ms(int ms) => SampleRate * ms / 1000;

// Exponential ADSR envelope value at sample index i.
double Adsr(int i, int na, int nd, int ns, int nr, double sus)
{
    if (i < na) return Math.Pow((double)i / na, 1.5);                                  // exponential attack
    i -= na;
    if (i < nd) { double x = (double)i / nd; return 1.0 - (1 - sus) * (1 - Math.Exp(-4 * x)); } // exp decay
    i -= nd;
    if (i < ns) return sus;                                                             // sustain
    i -= ns;
    return sus * Math.Exp(-5.0 * i / Math.Max(1, nr));                                 // exp release
}

double[] Scale(double[] samples, double factor)
{
    var result = new double[samples.Length];
    for (int i = 0; i < samples.Length; i++) result[i] = samples[i] * factor;
    return result;
}

double[] Silence(int durationMs) => new double[Ms(durationMs)];

double[] Concat(params double[][] arrays)
{
    var result = new double[arrays.Sum(a => a.Length)];
    int offset = 0;
    foreach (var arr in arrays)
    {
        Array.Copy(arr, 0, result, offset, arr.Length);
        offset += arr.Length;
    }
    return result;
}

void WriteWav(string path, double[] samples)
{
    // Peak normalize to 92%, then apply tanh soft clipping for safety
    double peak  = samples.Max(s => Math.Abs(s));
    double scale = peak < 1e-10 ? 1.0 : 0.92 / peak;

    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);

    int dataSize = samples.Length * (BitsPerSample / 8);
    bw.Write("RIFF"u8); bw.Write(36 + dataSize); bw.Write("WAVE"u8);
    bw.Write("fmt "u8); bw.Write(16); bw.Write((short)1); bw.Write((short)Channels);
    bw.Write(SampleRate);
    bw.Write(SampleRate * Channels * BitsPerSample / 8);
    bw.Write((short)(Channels * BitsPerSample / 8));
    bw.Write((short)BitsPerSample);
    bw.Write("data"u8); bw.Write(dataSize);

    foreach (var s in samples)
        bw.Write((short)(Math.Tanh(s * scale) * 0.97 * short.MaxValue));

    Console.WriteLine($"    {Path.GetFileName(path)} ({samples.Length} samples, {samples.Length * 1000 / SampleRate}ms)");
}
