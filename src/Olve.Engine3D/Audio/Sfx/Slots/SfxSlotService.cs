using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Scenes;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Slots;

public sealed class SfxSlotService(
    SfxLibraryManager library,
    SoundManager soundManager,
    AudioMixer mixer,
    AudioSegment segment,
    ILogger<SfxSlotService> logger) : ISceneService
{
    private readonly Random _random = new();
    private readonly Dictionary<Id<SfxSlot>, RenderedSfx> _rendered = [];

    public int RenderedSlots => _rendered.Count;

    public Result RenderBoundSounds(SfxLibraryContent content)
    {
        foreach (var slot in library.Slots)
        {
            var sound = SfxSlotResolver.Resolve(slot, content).Sound;
            var panel = sound is null ? null : content.Panels.FirstOrDefault(panel => panel.Id == sound.Panel);
            _rendered.TryGetValue(slot.Id, out var current);
            if (current is not null && ReferenceEquals(current.Sound, sound) && ReferenceEquals(current.Panel, panel))
            {
                continue;
            }

            if (current is not null)
            {
                _ = soundManager.Delete(current.Id);
                _rendered.Remove(slot.Id);
            }

            if (sound is null || panel is null)
            {
                continue;
            }

            if (SfxRendering.Render(soundManager, sound, panel, segment).TryPickProblems(out var problems, out var rendered))
            {
                logger.LogError("Failed to render sound '{Sound}' for SFX slot '{Slot}': {Problems}",
                    sound.Name, slot.Name, string.Join("; ", problems.Select(problem => problem.ToBriefString())));
                continue;
            }

            _rendered[slot.Id] = rendered;
            logger.LogDebug("Rendered sound '{Sound}' for SFX slot '{Slot}'", sound.Name, slot.Name);
        }

        return Result.Success();
    }

    public bool IsRendering(Id<SfxSlot> slotId) =>
        _rendered.TryGetValue(slotId, out var rendered) && soundManager.IsRendering(rendered.Id);

    public Result<VoiceId?> Play(Id<SfxSlot> slotId)
    {
        if (library.Slot(slotId) is not { } slot)
        {
            return new ResultProblem("There's no SFX slot {0}", slotId);
        }

        var state = library.SlotState(slot);
        if (state.Sound is null)
        {
            logger.LogDebug("SFX slot '{Slot}' is unbound; nothing plays", slot.Name);
            return Result<VoiceId?>.Success(null);
        }

        if (!_rendered.TryGetValue(slotId, out var rendered))
        {
            return new ResultProblem("SFX slot '{0}' is bound to sound '{1}', which failed to render", slot.Name, state.Sound.Name);
        }

        if (soundManager.IsRendering(rendered.Id))
        {
            return new ResultProblem("SFX slot '{0}' is bound to sound '{1}', which is still rendering", slot.Name, state.Sound.Name);
        }

        if (mixer.FindBus(slot.Bus).TryPickProblems(out var problems, out var bus))
        {
            return problems.Prepend("SFX slot '{0}' can't play", slot.Name);
        }

        var settings = SfxRendering.Vary(state.Variation, _random.NextDouble() * 2 - 1, _random.NextDouble() * 2 - 1);
        if (mixer.Play(rendered.Id, bus, settings, segment).TryPickProblems(out problems, out var voice))
        {
            return problems;
        }

        logger.LogDebug("Playing SFX slot '{Slot}' as voice {VoiceId} on bus '{Bus}'", slot.Name, voice, slot.Bus);
        return Result<VoiceId?>.Success(voice);
    }
}
