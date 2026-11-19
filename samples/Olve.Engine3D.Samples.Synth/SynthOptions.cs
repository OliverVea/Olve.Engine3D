using Olve.Engine3D.Samples.Synth.Rack;

namespace Olve.Engine3D.Samples.Synth;

/// <summary>How the host sets the synth up: how many octaves the piano has, four on a wide screen and three on a narrow or touch one.</summary>
public sealed record SynthOptions(int Octaves = RackLayout.WideOctaves);
