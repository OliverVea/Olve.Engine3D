using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Scenes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Slots;

public sealed class SfxPreviewService(
    SfxLibraryManager library,
    SoundManager soundManager,
    AudioMixer mixer,
    AudioSegment segment) : ISceneService
{
    private readonly Dictionary<Id<LibrarySound>, RenderedSfx> _rendered = [];

    public int RenderedPreviews => _rendered.Count;

    public Result<VoiceId> Preview(Id<LibrarySound> soundId, BusId bus, VoiceSettings settings)
    {
        if (Render(soundId).TryPickProblems(out var problems, out var rendered))
        {
            return problems;
        }

        return soundManager.IsRendering(rendered.Id)
            ? new ResultProblem("Sound '{0}' is still rendering; preview it again once it has", rendered.Sound.Name)
            : mixer.Play(rendered.Id, bus, settings, segment);
    }

    public Result Prepare(Id<LibrarySound> soundId) => Render(soundId).ToEmptyResult();

    private Result<RenderedSfx> Render(Id<LibrarySound> soundId)
    {
        if (library.Sound(soundId) is not { } sound || library.Panel(sound.Panel) is not { } panel)
        {
            return new ResultProblem("There's no sound {0} in the SFX library", soundId);
        }

        if (!_rendered.TryGetValue(soundId, out var rendered))
        {
            if (SfxRendering.Render(soundManager, sound, panel, segment).TryPickProblems(out var problems, out rendered))
            {
                return problems;
            }

            _rendered[soundId] = rendered;
        }

        return rendered;
    }

    public bool IsReady(Id<LibrarySound> soundId) =>
        _rendered.TryGetValue(soundId, out var rendered) && !soundManager.IsRendering(rendered.Id);

    public Result DropStalePreviews(SfxLibraryContent content)
    {
        foreach (var (id, rendered) in _rendered.ToList())
        {
            var sound = content.Sounds.FirstOrDefault(sound => sound.Id == id);
            var panel = sound is null ? null : content.Panels.FirstOrDefault(panel => panel.Id == sound.Panel);
            if (!ReferenceEquals(sound, rendered.Sound) || !ReferenceEquals(panel, rendered.Panel))
            {
                _ = soundManager.Delete(rendered.Id);
                _rendered.Remove(id);
            }
        }

        return Result.Success();
    }
}
