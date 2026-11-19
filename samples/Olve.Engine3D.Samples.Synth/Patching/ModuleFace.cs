using System.Collections.Immutable;

namespace Olve.Engine3D.Samples.Synth.Patching;

/// <summary>The kind of job a module does: it sets the colour of its card's stripe and the column the card stands in.</summary>
public enum ModuleGroup
{
    Source,
    Modulation,
    Shape,
    Effects,
    Output,
    Utility,
}

/// <summary>A control on a strip of a module's face, bound to one of its parameters; each has its CV jack under it.</summary>
public abstract record FaceControl(string Parameter)
{
    /// <summary>A knob; a big one, for the control a module is mostly turned by, has a metal insert in its cap.</summary>
    public sealed record Knob(string Parameter, bool Big = false) : FaceControl(Parameter);

    /// <summary>A slider moved up and down in its slot.</summary>
    public sealed record Fader(string Parameter) : FaceControl(Parameter);

    /// <summary>A push button for a switch with two positions, such as a step's gate: each press flips it, and its LED is lit while it is on.</summary>
    public sealed record Toggle(string Parameter) : FaceControl(Parameter);
}

/// <summary>A readout of a module's own on a strip, besides its knobs: the line input's picker, or the output's level meter.</summary>
public enum FaceDisplay
{
    None,

    /// <summary>A small green segment display of the input playing, with ◀ ▶ buttons to step through the inputs.</summary>
    InputPicker,

    /// <summary>An amber bar of lights for the output's level.</summary>
    LevelMeter,

    /// <summary>Over each of the strip's buttons, a light for its step, lit while the sequencer plays that step.</summary>
    StepLights,

    /// <summary>A small green segment display naming the quantizer's scale.</summary>
    ScaleReadout,
}

/// <summary>
/// One line of a module's face, read left to right as the signal goes: its inputs, then its controls, then its outputs,
/// by the names of the module's jacks and parameters (an empty name leaves a slot free, to line jacks up across strips).
/// A title names the strip when a face has several (VCO 1, VCO 2).
/// </summary>
public sealed record FaceStrip(
    string? Title,
    ImmutableArray<string> Inputs,
    ImmutableArray<FaceControl> Controls,
    ImmutableArray<string> Outputs,
    FaceDisplay Display = FaceDisplay.None);

/// <summary>
/// What a module's card shows and how: its group, and its strips from the top. The rack lays cards out from their faces
/// alone; a module whose definition gives none gets <see cref="ModuleFaces.Default"/>.
/// </summary>
public sealed record ModuleFace(ModuleGroup Group, ImmutableArray<FaceStrip> Strips);

public static class ModuleFaces
{
    /// <summary>The module's face: its definition's own, or one strip of its inputs, a knob for each parameter and its outputs.</summary>
    public static ModuleFace Of(ModuleDefinition definition) => definition.Face ?? Default(definition);

    public static ModuleFace Default(ModuleDefinition definition) => new(GroupOf(definition.Kind),
    [
        new FaceStrip(null,
            [.. definition.Jacks.Where(j => j is { Direction: JackDirection.Input, IsKnobInput: false }).Select(j => j.Name)],
            [.. definition.Parameters.Select(p => (FaceControl)new FaceControl.Knob(p.Name))],
            [.. definition.Jacks.Where(j => j.Direction == JackDirection.Output).Select(j => j.Name)],
            definition.Kind switch
            {
                ModuleKind.LineIn => FaceDisplay.InputPicker,
                ModuleKind.Output => FaceDisplay.LevelMeter,
                _ => FaceDisplay.None,
            }),
    ]);

    /// <summary>The group a kind of module belongs to, for a face that doesn't say.</summary>
    public static ModuleGroup GroupOf(ModuleKind kind) => kind switch
    {
        ModuleKind.Oscillators or ModuleKind.LineIn or ModuleKind.Keyboard or ModuleKind.Sequencer => ModuleGroup.Source,
        ModuleKind.Lfo or ModuleKind.Random => ModuleGroup.Modulation,
        ModuleKind.Filter or ModuleKind.Vca or ModuleKind.Envelope => ModuleGroup.Shape,
        ModuleKind.Drive or ModuleKind.Phaser or ModuleKind.Delay or ModuleKind.Reverb => ModuleGroup.Effects,
        ModuleKind.Mixer or ModuleKind.Output => ModuleGroup.Output,
        ModuleKind.Quantizer => ModuleGroup.Utility,
        _ => ModuleGroup.Utility,
    };
}
