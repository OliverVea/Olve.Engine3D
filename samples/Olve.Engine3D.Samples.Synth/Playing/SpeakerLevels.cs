using Olve.Engine3D.Scenes;
using Olve.Engine3D.Time;

namespace Olve.Engine3D.Samples.Synth.Playing;

public enum Speaker
{
    Left,
    Right,
}

/// <summary>
/// Each frame, the left and right speakers' peaks since the last frame, read back from the audio and kept as
/// <see cref="OutputMeter"/> readings, for the speakers and OUT's meter to show each side's level.
/// </summary>
public sealed class SpeakerLevelsService(SynthRack rack, DeltaTimeService time) : ISceneService
{
    private readonly float[] _peaks = new float[2];
    private readonly float[] _readings = [OutputMeter.Floor - 1, OutputMeter.Floor - 1];

    /// <summary>The speaker's level now, in decibels of its peak, as <see cref="OutputMeter.Step"/> keeps it.</summary>
    public float Reading(Speaker speaker) => _readings[(int)speaker];

    /// <summary>How many of a ring of <see cref="OutputMeter.Lights"/> lights the speaker's level lights.</summary>
    public float Lit(Speaker speaker) => OutputMeter.Lit(Reading(speaker));

    public Result Update()
    {
        var read = rack.Audio.ReadSpeakers(_peaks);
        var seconds = (float)time.RawDeltaTime.TotalSeconds;
        for (var i = 0; i < _readings.Length; i++)
        {
            _readings[i] = OutputMeter.Step(_readings[i], i < read ? _peaks[i] : 0, seconds);
        }

        return Result.Success();
    }
}
