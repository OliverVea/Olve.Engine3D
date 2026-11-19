namespace Olve.Engine3D.Audio.Synthesis;

/// <summary>A node of a <see cref="Patch"/>. Its parameters can be modulated: a connection adds its signal to them.</summary>
public abstract record PatchNode
{
    // Closed: the cases below are the only ones.
    private PatchNode(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>A band-limited waveform from -1 to 1. Parameters: <c>Frequency</c> (Hz).</summary>
    public sealed record Oscillator(string Name, Waveform Waveform, float Frequency) : PatchNode(Name)
    {
        public const string FrequencyParameter = "Frequency";
    }

    /// <summary>
    /// A steady signal: its <c>Value</c>, plus anything patched into it, as Web Audio's constant source. Parameters:
    /// <c>Value</c>.
    /// </summary>
    public sealed record Constant(string Name, float Value) : PatchNode(Name)
    {
        public const string ValueParameter = "Value";
    }

    /// <summary>White noise from -1 to 1, the same for the same seed.</summary>
    public sealed record Noise(string Name, int Seed) : PatchNode(Name);

    /// <summary>
    /// A biquad filter of its audio input, as Web Audio's. Parameters: <c>Cutoff</c> (Hz), <c>Q</c>, <c>Gain</c> (<see cref="GainDb"/>, in dB,
    /// for peaking and the shelves).
    /// </summary>
    public sealed record Filter(string Name, FilterType Type, float Cutoff, float Q, float GainDb = 0) : PatchNode(Name)
    {
        public const string CutoffParameter = "Cutoff";
        public const string QParameter = "Q";
        public const string GainParameter = "Gain";
    }

    /// <summary>
    /// Its audio input times <c>Drive</c>, bent back into -1 to 1 by the curve, as Web Audio's wave shaper with 4x
    /// oversampling. Parameters: <c>Drive</c>.
    /// </summary>
    public sealed record Distortion(string Name, DistortionCurve Curve, float Drive) : PatchNode(Name)
    {
        public const string DriveParameter = "Drive";
    }

    /// <summary>
    /// Its audio input times <c>Amount</c>; with an amount of 0 and a signal into it, the two multiply (ring modulation).
    /// Parameters: <c>Amount</c>.
    /// </summary>
    public sealed record Gain(string Name, float Amount) : PatchNode(Name)
    {
        public const string AmountParameter = "Amount";
    }

    /// <summary>
    /// A control signal from 0 to 1: rises to 1 over the attack once the gate opens, falls to the sustain level over
    /// the decay, and falls from where it is to 0 over the release once the gate closes. A signal patched into
    /// <c>Gate</c> is the gate (open above 0.5); with nothing patched, the note is. Parameters: <c>Gate</c>.
    /// </summary>
    public sealed record Envelope(string Name, TimeSpan Attack, TimeSpan Decay, float Sustain, TimeSpan Release) : PatchNode(Name)
    {
        public const string GateParameter = "Gate";
    }
}
