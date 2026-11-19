using System.Numerics;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;
using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Samples.Synth.Space;

/// <summary>One light's settings, each from 0 to 1: how bright, and its colour's hue and saturation.</summary>
public readonly record struct LightSettings(float Intensity, float Hue, float Saturation)
{
    /// <summary>The light's colour at full value, times its intensity.</summary>
    public Vector3 Color => Hsv(Hue, Saturation) * Intensity;

    private static Vector3 Hsv(float hue, float saturation)
    {
        var h = (hue - MathF.Floor(hue)) * 6;
        var rgb = new Vector3(
            float.Clamp(MathF.Abs(h - 3) - 1, 0, 1),
            float.Clamp(2 - MathF.Abs(h - 2), 0, 1),
            float.Clamp(2 - MathF.Abs(h - 4), 0, 1));
        return Vector3.Lerp(Vector3.One, rgb, float.Clamp(saturation, 0, 1));
    }
}

/// <summary>One light's settings as its knobs on the LIGHTS module read, and which of them a cable drives.</summary>
public readonly record struct LightReading(LightSettings Settings, bool IntensityPatched = false, bool HuePatched = false, bool SaturationPatched = false);

/// <summary>
/// The room's two lights, left and right, as the LIGHTS module's knobs set them. A part no cable drives drifts slowly
/// round its knob, so the room is never quite still; a part a signal drives follows the signal alone.
/// </summary>
public sealed class RackLights
{
    public static readonly LightSettings LeftRest = new(0.9f, 0.08f, 0.35f);
    public static readonly LightSettings RightRest = new(0.75f, 0.58f, 0.2f);

    private double _seconds;

    public LightSettings Left { get; private set; } = LeftRest;

    public LightSettings Right { get; private set; } = RightRest;

    public void Update(TimeSpan elapsed, LightReading left, LightReading right)
    {
        _seconds += elapsed.TotalSeconds;
        Left = Drift(left, _seconds, 0);
        Right = Drift(right, _seconds, 2.1);
    }

    // A gentle swell of brightness and a small swing of hue, over tens of seconds
    private static LightSettings Drift(LightReading reading, double seconds, double phase)
    {
        var settings = reading.Settings;
        return settings with
        {
            Intensity = reading.IntensityPatched ? settings.Intensity : settings.Intensity * (1 + 0.08f * MathF.Sin((float)(seconds * 0.31 + phase))),
            Hue = reading.HuePatched ? settings.Hue : settings.Hue + 0.025f * MathF.Sin((float)(seconds * 0.13 + phase * 1.7)),
        };
    }

    /// <summary>
    /// A light as the rack's first LIGHTS module has it, each knob's value read back from the audio with its CV; at
    /// rest when the rack has none.
    /// </summary>
    public static LightReading Read(SynthRack rack, LightSide side)
    {
        if (rack.Patch.Modules.FirstOrDefault(m => m.Kind == ModuleKind.Lights) is not { } lights)
        {
            return new LightReading(side == LightSide.Left ? LeftRest : RightRest);
        }

        float Value(string part) => rack.Audio.ReadKnob(lights.Id, Modules.LightKnob(side, part)) is var value && float.IsFinite(value)
            ? value
            : rack.Patch.GetValue(lights.Id, Modules.LightKnob(side, part));

        bool Patched(string part) => rack.Patch.CableInto(new JackRef(lights.Id, lights.Definition.FindParameter(Modules.LightKnob(side, part))!.CvJack)) is not null;

        return new LightReading(
            new LightSettings(Value(Modules.Intensity), Value(Modules.Hue), Value(Modules.Saturation)),
            Patched(Modules.Intensity), Patched(Modules.Hue), Patched(Modules.Saturation));
    }
}

/// <summary>Each frame, the room's lights follow the LIGHTS module.</summary>
public sealed class RackLightsService(SynthRack rack, DeltaTimeService time) : ISceneService
{
    public RackLights Lights { get; } = new();

    public Result Update()
    {
        Lights.Update(time.RawDeltaTime, RackLights.Read(rack, LightSide.Left), RackLights.Read(rack, LightSide.Right));
        return Result.Success();
    }
}
