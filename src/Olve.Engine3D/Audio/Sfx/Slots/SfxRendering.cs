using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Slots;

public static class SfxRendering
{
    public static Dictionary<string, float> InputValues(LibrarySound sound, PatchPanel panel) => panel.Patch.Parameters
        .Where(input => sound.Inputs.ContainsKey(input.Id))
        .ToDictionary(input => input.Name, input => sound.Inputs[input.Id]);

    public static VoiceSettings Vary(SfxVariation variation, double pitchRoll, double gainRoll) => new(
        Gain: (float)double.Pow(10, gainRoll * variation.GainDb / 20),
        Pitch: (float)double.Pow(2, pitchRoll * variation.PitchSemitones / 12));

    internal static Result<RenderedSfx> Render(SoundManager soundManager, LibrarySound sound, PatchPanel panel, Id<AudioSegment> segment) =>
        soundManager.Register(panel.Patch, sound.Length, segment, InputValues(sound, panel))
            .TryPickProblems(out var problems, out var id)
            ? problems.Prepend("Failed to render sound '{0}'", sound.Name)
            : new RenderedSfx(sound, panel, id);
}
