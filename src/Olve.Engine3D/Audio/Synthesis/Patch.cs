using System.Collections.Immutable;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Synthesis;

/// <summary>
/// A sound as data: named nodes, the connections between them, the parameters the game may set, and the node heard.
/// A node sums the audio connected into it; a connection into a parameter adds its signal to the parameter's value.
/// </summary>
public sealed record Patch(
    ImmutableArray<PatchNode> Nodes,
    ImmutableArray<PatchConnection> Connections,
    ImmutableArray<PatchParameter> Parameters,
    string Output);

/// <summary>From's output into To's audio input, or, with a parameter, into that parameter of To.</summary>
public sealed record PatchConnection(string From, string To, string? Parameter = null);

/// <summary>A parameter the game may set: a name for one node's parameter, replacing its value.</summary>
public sealed record PatchParameter(Id<PatchInput> Id, string Name, string Node, string Parameter);

public sealed class PatchInput;

public enum Waveform
{
    Sine,
    Square,
    Sawtooth,
    Triangle,
}

/// <summary>How a <see cref="PatchNode.Distortion"/> bends its driven input back into -1 to 1.</summary>
public enum DistortionCurve
{
    /// <summary>tanh: rounds off smoothly, warm overdrive.</summary>
    SoftClip,

    /// <summary>Cuts flat at -1 and 1: harsh, square-ish fuzz.</summary>
    HardClip,

    /// <summary>Reflects back at -1 and 1, again and again: metallic, buzzy wavefolding.</summary>
    Fold,
}

/// <summary>Web Audio's biquad filter types.</summary>
public enum FilterType
{
    Lowpass,
    Highpass,
    Bandpass,
    Notch,

    /// <summary>Boosts or cuts by the gain around the cutoff.</summary>
    Peaking,

    /// <summary>Shifts the phase around the cutoff, leaving the level alone.</summary>
    Allpass,

    /// <summary>Boosts or cuts by the gain below the cutoff.</summary>
    Lowshelf,

    /// <summary>Boosts or cuts by the gain above the cutoff.</summary>
    Highshelf,
}
