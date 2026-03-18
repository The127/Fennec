// One-time tool to generate WAV sound files for voice event feedback.
// Run: dotnet run --project Tools/SoundGenerator
// Output: Fennec.App/Assets/Sounds/{pack}/{effect}.wav
//
// Sound design approach:
//   - Additive synthesis with per-harmonic decay (bright attack → warm sustain,
//     modelling how real acoustic instruments lose upper modes over time)
//   - Subtle pitch envelope on notes (start slightly sharp, settle — organic "struck" feel)
//   - Phase-continuous arpeggio with logarithmic portamento
//   - Logarithmic frequency chirps for mute/deafen effects
//   - Moorer-variant Schroeder reverb with LP-damped comb feedback for natural tail
//   - Tanh soft clipping + peak normalization

const int SampleRate    = 48000;
const int BitsPerSample = 16;
const int Channels      = 1;

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

    // Harmonic profiles — moderate upper partials that per-harmonic decay will shape.
    // The initial spectrum is richer than final; decay sculpts the brightness naturally.
    double[] richH = [1.0, 0.40, 0.15, 0.05];
    double[] medH  = [1.0, 0.28, 0.07];
    double[] thinH = [1.0, 0.12];

    double[] mainH = isMinimal ? thinH : (isSoft ? medH : richH);

    // Per-harmonic decay rate: controls how fast upper harmonics die relative to fundamental.
    // Higher = brighter attack that settles faster into a warm pure tone.
    // This is the key parameter for organic, instrument-like quality.
    double harmDecay = isMinimal ? 3 : (isSoft ? 10 : 6);

    // Reverb (Moorer LP-comb: tail naturally darkens like a real room)
    double reverbWet  = isMinimal ? 0.0 : (isSoft ? 0.22 : 0.16);
    double reverbRoom = isSoft ? 0.82 : 0.76;

    // Pitch envelope: cents sharp at onset, quadratic ease-out to target.
    // Simulates the "settling" of a struck bar or plucked string.
    double pitchCents = isMinimal ? 0 : (isSoft ? 12 : 20);
    int    pitchMs    = isMinimal ? 0 : (isSoft ? 35 : 25);

    // ── Join: ascending C4→E4→G4 major arpeggio ──────────────────────────────
    double[] joinFreqs = isMinimal ? [329.63]                    : [261.63, 329.63, 392.00]; // C4, E4, G4
    int[]    joinDurs  = isMinimal ? [140]                       : (isSoft ? [150, 140, 200] : [120, 110, 160]);
    int    joinAtk   = isMinimal ? 12 : (isSoft ? 28 : 20);
    int    joinDec   = isMinimal ? 30 : (isSoft ? 50 : 38);
    int    joinRel   = isMinimal ? 45 : (isSoft ? 80 : 60);
    int    joinGlide = isMinimal ? 0  : (isSoft ? 32 : 22);
    double joinSus   = 0.55;

    {
        var raw = isMinimal
            ? Note(joinFreqs[0], joinDurs[0], joinAtk, joinDec, joinSus, joinRel,
                   mainH, harmDecay, pitchCents, pitchMs)
            : Arpeggio(joinFreqs, joinDurs, joinGlide, joinAtk, joinDec, joinSus, joinRel,
                       mainH, harmDecay, pitchCents, pitchMs);
        raw = Reverb(raw, reverbWet, reverbRoom);
        WriteWav(Path.Combine(dir, "join.wav"), raw);
    }

    // ── Leave: descending G4→E4→C4 ───────────────────────────────────────────
    double[] leaveFreqs = isMinimal ? [261.63]                   : [392.00, 329.63, 261.63];
    int[]    leaveDurs  = isMinimal ? [140]                      : (isSoft ? [160, 150, 160] : [130, 120, 140]);
    int leaveRel = isMinimal ? 50 : (isSoft ? 90 : 70);

    {
        var raw = isMinimal
            ? Note(leaveFreqs[0], leaveDurs[0], joinAtk, joinDec, joinSus * 0.85, leaveRel,
                   mainH, harmDecay, pitchCents * 0.5, pitchMs)
            : Arpeggio(leaveFreqs, leaveDurs, joinGlide, joinAtk, joinDec, joinSus * 0.85, leaveRel,
                       mainH, harmDecay, pitchCents * 0.5, pitchMs);
        raw = Reverb(raw, reverbWet, reverbRoom);
        WriteWav(Path.Combine(dir, "leave.wav"), Scale(raw, 0.85));
    }

    // ── Mute: gentle downward glide (~minor 3rd interval) ────────────────────
    double muteHi  = isMinimal ? 370 : (isSoft ? 355 : 380);
    double muteLo  = isMinimal ? 280 : (isSoft ? 265 : 280);
    int    muteDur = isMinimal ? 110 : (isSoft ? 160 : 140);
    int    muteAtk = isMinimal ? 10 : (isSoft ? 16 : 12);
    int    muteRel = isMinimal ? 45 : (isSoft ? 75 : 60);

    {
        var raw = Chirp(muteHi, muteLo, muteDur, muteAtk, muteRel, medH, harmDecay);
        raw = Reverb(raw, reverbWet * 0.3, reverbRoom);
        WriteWav(Path.Combine(dir, "mute.wav"), Scale(raw, 0.70));
    }

    // ── Unmute: gentle upward glide ──────────────────────────────────────────
    double unmuteHi  = isMinimal ? 400 : (isSoft ? 385 : 410);
    double unmuteLo  = isMinimal ? 280 : (isSoft ? 265 : 280);
    int    unmuteDur = isMinimal ? 110 : (isSoft ? 160 : 140);

    {
        var raw = Chirp(unmuteLo, unmuteHi, unmuteDur, muteAtk, muteRel, medH, harmDecay);
        raw = Reverb(raw, reverbWet * 0.3, reverbRoom);
        WriteWav(Path.Combine(dir, "unmute.wav"), Scale(raw, 0.70));
    }

    // ── Deafen: two descending glides (settling, covering) ───────────────────
    double def1Hi = isMinimal ? 350 : (isSoft ? 340 : 360);
    double def1Lo = isMinimal ? 250 : (isSoft ? 240 : 250);
    int    def1D  = isMinimal ? 90  : (isSoft ? 130 : 115);
    int    defGap = isMinimal ? 35  : (isSoft ? 45  : 38);
    double def2Hi = isMinimal ? 310 : (isSoft ? 300 : 320);
    double def2Lo = isMinimal ? 220 : (isSoft ? 210 : 220);
    int    def2D  = isMinimal ? 80  : (isSoft ? 120 : 105);

    {
        var c1  = Chirp(def1Hi, def1Lo, def1D, muteAtk, muteRel, medH, harmDecay);
        var c2  = Chirp(def2Hi, def2Lo, def2D, muteAtk, muteRel, medH, harmDecay);
        var raw = Concat(c1, Silence(defGap), c2);
        raw = Reverb(raw, reverbWet * 0.45, reverbRoom);
        WriteWav(Path.Combine(dir, "deafen.wav"), Scale(raw, 0.80));
    }

    // ── Undeafen: two ascending glides (opening, lifting) ────────────────────
    double und1Lo = isMinimal ? 240 : (isSoft ? 230 : 240);
    double und1Hi = isMinimal ? 350 : (isSoft ? 340 : 355);
    int    und1D  = isMinimal ? 85  : (isSoft ? 125 : 110);
    int    undGap = isMinimal ? 32  : (isSoft ? 42  : 35);
    double und2Lo = isMinimal ? 270 : (isSoft ? 260 : 270);
    double und2Hi = isMinimal ? 400 : (isSoft ? 390 : 410);
    int    und2D  = isMinimal ? 90  : (isSoft ? 130 : 115);

    {
        var c1  = Chirp(und1Lo, und1Hi, und1D, muteAtk, muteRel, medH, harmDecay);
        var c2  = Chirp(und2Lo, und2Hi, und2D, muteAtk, muteRel, medH, harmDecay);
        var raw = Concat(c1, Silence(undGap), c2);
        raw = Reverb(raw, reverbWet * 0.45, reverbRoom);
        WriteWav(Path.Combine(dir, "undeafen.wav"), Scale(raw, 0.80));
    }

    Console.WriteLine($"  Generated pack: {packName}");
}

// ─── Synthesis ────────────────────────────────────────────────────────────────

// Single note with per-harmonic decay and pitch envelope.
// Per-harmonic decay is the core technique: harmonic h decays as exp(-h * rate * t),
// so higher partials die faster — bright onset settles to warm fundamental.
// This models real acoustic instruments without any post-processing.
double[] Note(double freqHz, int durationMs, int attackMs, int decayMs,
              double sustainLevel, int releaseMs, double[] harmonics,
              double harmonicDecayRate, double pitchCents = 0, int pitchEnvMs = 0)
{
    int n  = Ms(durationMs);
    int na = Ms(attackMs);
    int nd = Ms(decayMs);
    int nr = Ms(releaseMs);
    int ns = Math.Max(0, n - na - nd - nr);
    var buf   = new double[n];
    var phase = new double[harmonics.Length];
    double totalAmp = harmonics.Sum();
    int pitchSamples = Ms(pitchEnvMs);

    for (int i = 0; i < n; i++)
    {
        double t = (double)i / SampleRate;

        // Pitch envelope: start sharp, settle with quadratic ease-out
        double freq = freqHz;
        if (pitchCents != 0 && pitchSamples > 0 && i < pitchSamples)
        {
            double px = 1.0 - (double)i / pitchSamples;
            freq *= Math.Pow(2, pitchCents / 1200.0 * px * px);
        }

        // Phase accumulation (continuous — no clicks from pitch changes)
        for (int h = 0; h < harmonics.Length; h++)
            phase[h] += 2 * Math.PI * freq * (h + 1) / SampleRate;

        // Per-harmonic additive synthesis
        double wave = 0;
        for (int h = 0; h < harmonics.Length; h++)
        {
            double hDecay = Math.Exp(-h * harmonicDecayRate * t);
            wave += Math.Sin(phase[h]) * harmonics[h] * hDecay;
        }
        wave /= totalAmp;

        buf[i] = wave * Adsr(i, na, nd, ns, nr, sustainLevel);
    }
    return buf;
}

// Frequency chirp with per-harmonic decay and phase accumulation.
double[] Chirp(double startHz, double endHz, int durationMs,
               int attackMs, int releaseMs, double[] harmonics,
               double harmonicDecayRate)
{
    int n  = Ms(durationMs);
    int na = Ms(attackMs);
    int nr = Ms(releaseMs);
    var buf = new double[n];
    var ph  = new double[harmonics.Length];
    double totalAmp = harmonics.Sum();

    for (int i = 0; i < n; i++)
    {
        double t    = (double)i / SampleRate;
        double x    = (double)i / n;
        double freq = startHz * Math.Pow(endHz / startHz, x); // logarithmic sweep

        // Envelope: quadratic attack → gentle exponential body → smooth release
        double env;
        if (i < na)
        {
            double ax = (double)i / na;
            env = ax * ax;
        }
        else if (i > n - nr)
        {
            double rx = (double)(i - (n - nr)) / nr;
            env = Math.Exp(-2.5 * rx);
        }
        else
        {
            double sx = (double)(i - na) / Math.Max(1, n - na - nr);
            env = Math.Exp(-1.2 * sx);
        }

        for (int h = 0; h < harmonics.Length; h++)
            ph[h] += 2 * Math.PI * freq * (h + 1) / SampleRate;

        double wave = 0;
        for (int h = 0; h < harmonics.Length; h++)
        {
            double hDecay = Math.Exp(-h * harmonicDecayRate * t);
            wave += Math.Sin(ph[h]) * harmonics[h] * hDecay;
        }
        wave /= totalAmp;

        buf[i] = wave * env;
    }
    return buf;
}

// Multi-note arpeggio with per-note harmonic decay, pitch envelope, and portamento.
// Harmonic decay resets per note — each note gets its own bright attack that settles,
// like a mallet striking a new bar on a marimba.
double[] Arpeggio(double[] freqs, int[] durationsMsPerNote, int glideMs,
                  int attackMs, int decayMs, double sustainLevel, int releaseMs,
                  double[] harmonics, double harmonicDecayRate,
                  double pitchCents = 0, int pitchEnvMs = 0)
{
    int total = durationsMsPerNote.Select(Ms).Sum();
    var buf   = new double[total];
    var ph    = new double[harmonics.Length]; // phases carry across notes (no clicks)
    double totalAmp = harmonics.Sum();
    int pos = 0;

    for (int ni = 0; ni < freqs.Length; ni++)
    {
        double targetHz = freqs[ni];
        double prevHz   = ni == 0 ? targetHz : freqs[ni - 1];
        int ns  = Ms(durationsMsPerNote[ni]);
        int ng  = ni == 0 ? 0 : Math.Min(Ms(glideMs), ns / 3);
        int na  = Ms(attackMs);
        int nd  = Ms(decayMs);
        int nr  = Ms(releaseMs);
        int pitchSamples = Ms(pitchEnvMs);

        for (int i = 0; i < ns; i++)
        {
            double noteT = (double)i / SampleRate; // time within this note

            // Logarithmic portamento from previous note
            double freq = (ng > 0 && i < ng)
                ? prevHz * Math.Pow(targetHz / prevHz, (double)i / ng)
                : targetHz;

            // Per-note pitch envelope
            if (pitchCents != 0 && pitchSamples > 0 && i < pitchSamples)
            {
                double px = 1.0 - (double)i / pitchSamples;
                freq *= Math.Pow(2, pitchCents / 1200.0 * px * px);
            }

            // Envelope: full attack on first note, brief re-attack on subsequent
            double env;
            int noteAtk = ni == 0 ? na : na / 3;
            if (i < noteAtk)
            {
                env = ni == 0
                    ? Math.Pow((double)i / noteAtk, 2.0)
                    : sustainLevel + (1.0 - sustainLevel) * (double)i / noteAtk;
            }
            else if (i < noteAtk + nd)
            {
                double dx = (double)(i - noteAtk) / nd;
                env = 1.0 - (1.0 - sustainLevel) * (1.0 - Math.Exp(-3.0 * dx));
            }
            else if (i < ns - nr)
            {
                env = sustainLevel;
            }
            else
            {
                double rx = (double)(i - (ns - nr)) / nr;
                env = sustainLevel * Math.Exp(-3.0 * rx);
            }

            for (int h = 0; h < harmonics.Length; h++)
                ph[h] += 2 * Math.PI * freq * (h + 1) / SampleRate;

            // Per-note harmonic decay (resets each note for fresh bright attack)
            double wave = 0;
            for (int h = 0; h < harmonics.Length; h++)
            {
                double hDecay = Math.Exp(-h * harmonicDecayRate * noteT);
                wave += Math.Sin(ph[h]) * harmonics[h] * hDecay;
            }
            wave /= totalAmp;

            buf[pos + i] = wave * env;
        }
        pos += ns;
    }
    return buf;
}

// ─── Reverb ───────────────────────────────────────────────────────────────────

// Moorer-variant Schroeder reverb: LP-damped comb filters → all-pass diffusers.
// The LP filter inside each comb's feedback loop progressively darkens the tail
// with each recirculation — modelling how real room surfaces absorb high frequencies.
// This eliminates the metallic ringing of undamped Schroeder reverbs.
double[] Reverb(double[] input, double wetMix, double roomSize)
{
    if (wetMix <= 0) return input;

    // Comb delays: mutually prime, well-spaced for dense, smooth reflections
    int[] combDelays = [1117, 1367, 1637, 1901];
    double feedback  = 0.65 + roomSize * 0.12;
    double damp      = 0.3; // per-reflection HF absorption (0 = bright, 1 = very dark)

    // 3 all-pass filters for diffusion density (was 2 — 3 gives smoother result)
    int[] apDelays = [241, 557, 907];
    double apGain  = 0.6;

    int tailMs = (int)(200 + roomSize * 150);
    int outLen = input.Length + Ms(tailMs);
    var wet    = new double[outLen];

    // 4 parallel LP-comb filters
    foreach (int d in combDelays)
    {
        var cbuf    = new double[d];
        int cp      = 0;
        double lpSt = 0; // 1-pole LP state in feedback path

        for (int i = 0; i < outLen; i++)
        {
            double x       = i < input.Length ? input[i] : 0.0;
            double delayed = cbuf[cp];
            lpSt     = delayed * (1 - damp) + lpSt * damp; // LP-filtered feedback
            cbuf[cp] = x + lpSt * feedback;
            cp       = (cp + 1) % d;
            wet[i]  += delayed * 0.25;
        }
    }

    // 3 serial all-pass filters
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

    // Dry/wet mix
    var result = new double[outLen];
    for (int i = 0; i < outLen; i++)
    {
        double dry = i < input.Length ? input[i] : 0.0;
        result[i]  = dry * (1 - wetMix) + wet[i] * wetMix;
    }
    return result;
}

// ─── Utilities ────────────────────────────────────────────────────────────────

int Ms(int ms) => SampleRate * ms / 1000;

// ADSR with quadratic attack and gentle exponential decay/release.
double Adsr(int i, int na, int nd, int ns, int nr, double sus)
{
    if (i < na) return Math.Pow((double)i / na, 2.0);
    i -= na;
    if (i < nd) { double x = (double)i / nd; return 1.0 - (1 - sus) * (1 - Math.Exp(-3 * x)); }
    i -= nd;
    if (i < ns) return sus;
    i -= ns;
    return sus * Math.Exp(-3.0 * i / Math.Max(1, nr));
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
