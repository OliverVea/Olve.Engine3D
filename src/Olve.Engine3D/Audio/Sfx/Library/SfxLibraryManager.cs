using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Validation;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Library;

public sealed class SfxLibraryManager(SfxLibraryStore store, SfxLibraryOptions options, ILogger<SfxLibraryManager> logger)
{
    private readonly ImmutableDictionary<Id<SfxSlot>, SfxSlot> _slots = options.Slots.ToImmutableDictionary(slot => slot.Id);
    private bool _loaded;

    public SfxLibraryContent Content { get; private set; } = SfxLibraryContent.Empty;

    public IReadOnlyList<SfxSlot> Slots => options.Slots;

    public Event<SfxLibraryContent> OnChanged { get; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var stored = await store.ReadAsync(cancellationToken);
        var seeded = SfxExampleSeeder.Seed(stored, options.Examples);
        Content = seeded;
        _loaded = true;
        LogSeeded(stored, seeded);
        WarnAboutBindings(seeded);
        logger.LogInformation("Loaded the SFX library: {Panels} panel(s), {Sounds} sound(s), {Bindings} binding(s)",
            seeded.Panels.Length, seeded.Sounds.Length, seeded.Bindings.Length);

        if (store.CanSave && !ReferenceEquals(seeded, stored))
        {
            _ = await store.SaveAsync(seeded);
        }

        OnChanged.Invoke(Content);
    }

    public Task<Result> Flush() => store.Flush();

    public Result CheckLoaded() => _loaded
        ? Result.Success()
        : new ResultProblem("The SFX library is still loading");

    public PatchPanel? Panel(Id<PatchPanel> id) => Content.Panels.FirstOrDefault(panel => panel.Id == id);

    public LibrarySound? Sound(Id<LibrarySound> id) => Content.Sounds.FirstOrDefault(sound => sound.Id == id);

    public SfxSlot? Slot(Id<SfxSlot> id) => _slots.GetValueOrDefault(id);

    public SfxSlotState SlotState(SfxSlot slot) => SfxSlotResolver.Resolve(slot, Content);

    public IEnumerable<SfxSlot> SlotsBoundTo(Id<LibrarySound> sound) => Content.Bindings
        .Where(binding => binding.Sound == sound)
        .Select(binding => Slot(binding.Slot))
        .OfType<SfxSlot>();

    public Result<LibrarySound> CreateSound(
        Id<PatchPanel> panelId, string name, TimeSpan length, ImmutableDictionary<Id<PatchInput>, float> inputs)
    {
        if (CheckChangeable().TryPickProblems(out var problems))
        {
            return problems;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return new ResultProblem("A sound needs a name");
        }

        if (Panel(panelId) is not { } panel)
        {
            return new ResultProblem("There's no panel {0} in the SFX library", panelId);
        }

        var sound = new LibrarySound(Id.New<LibrarySound>(), name, panelId, length, inputs);
        if (LibrarySoundValidator.For(Panel).Validate(sound).TryPickProblems(out problems))
        {
            return problems;
        }

        logger.LogInformation("Created SFX sound '{Name}' ({SoundId}) from panel '{Panel}' ({PanelId}), {Length} s long, inputs {Inputs}",
            name, sound.Id, panel.Name, panel.Id, length.TotalSeconds, DescribeInputs(sound, panel));
        Change(Content with { Sounds = Content.Sounds.Add(sound) });
        return sound;
    }

    public Result<SfxDeletion> PlanSoundDeletion(Id<LibrarySound> id) => SfxDeletionPlanner.ForSound(Content, id);

    public Result<SfxDeletion> PlanPanelDeletion(Id<PatchPanel> id) => SfxDeletionPlanner.ForPanel(Content, id);

    public Result<SfxDeletion> Delete(SfxDeletion deletion)
    {
        if (CheckChangeable().TryPickProblems(out var problems))
        {
            return problems;
        }

        foreach (var panel in deletion.Panels)
        {
            logger.LogInformation("Deleted SFX panel '{Name}' ({PanelId})", panel.Name, panel.Id);
        }

        foreach (var sound in deletion.Sounds)
        {
            logger.LogInformation("Deleted SFX sound '{Name}' ({SoundId})", sound.Name, sound.Id);
        }

        if (deletion.UnboundSlots.Length > 0)
        {
            logger.LogInformation("Unbound {Count} SFX slot(s): {Slots}", deletion.UnboundSlots.Length, string.Join(", ", deletion.UnboundSlots));
        }

        Change(SfxDeletionPlanner.Apply(Content, deletion));
        return deletion;
    }

    public Result Bind(Id<SfxSlot> slotId, Id<LibrarySound> soundId, float? pitchSemitones, float? gainDb)
    {
        if (CheckChangeable().TryPickProblems(out var problems))
        {
            return problems;
        }

        if (Slot(slotId) is not { } slot)
        {
            return new ResultProblem("There's no SFX slot {0}", slotId);
        }

        if (Sound(soundId) is not { } sound)
        {
            return new ResultProblem("There's no sound {0} in the SFX library", soundId);
        }

        if (pitchSemitones is < 0 || gainDb is < 0)
        {
            return new ResultProblem("A variation can't be negative");
        }

        var previous = Content.Bindings.FirstOrDefault(binding => binding.Slot == slotId);
        var binding = new SfxBinding(slotId, soundId, pitchSemitones ?? previous?.PitchSemitones, gainDb ?? previous?.GainDb);
        Change(Content with
        {
            Bindings = previous is null ? Content.Bindings.Add(binding) : Content.Bindings.Replace(previous, binding),
        });

        logger.LogInformation("Bound SFX slot '{Slot}' to sound '{Sound}' ({SoundId}), variation {Variation}",
            slot.Name, sound.Name, sound.Id, SlotState(slot).Variation);
        return Result.Success();
    }

    public Result Unbind(Id<SfxSlot> slotId)
    {
        if (CheckChangeable().TryPickProblems(out var problems))
        {
            return problems;
        }

        if (Slot(slotId) is not { } slot)
        {
            return new ResultProblem("There's no SFX slot {0}", slotId);
        }

        if (Content.Bindings.FirstOrDefault(binding => binding.Slot == slotId) is not { } previous)
        {
            return new ResultProblem("SFX slot '{0}' isn't bound", slot.Name);
        }

        logger.LogInformation("Unbound SFX slot '{Slot}'", slot.Name);
        Change(Content with { Bindings = Content.Bindings.Remove(previous) });
        return Result.Success();
    }

    public byte[] Export() => SfxLibraryCodec.Serialize(Content);

    public Result<SfxImportPlan> PlanImport(ReadOnlySpan<byte> bytes) => SfxLibraryImport.Plan(Content, bytes);

    public Result<SfxImport> Import(ReadOnlySpan<byte> bytes)
    {
        if (CheckChangeable().TryPickProblems(out var problems)
            || PlanImport(bytes).TryPickProblems(out problems, out var plan))
        {
            return problems;
        }

        logger.LogInformation("Imported an SFX library: {Summary}", plan.Summary);
        WarnAboutBindings(plan.Merged);
        Change(plan.Merged);
        return plan.Summary;
    }

    private Result CheckChangeable()
    {
        if (CheckLoaded().TryPickProblems(out var problems))
        {
            return problems;
        }

        return store.CanSave
            ? Result.Success()
            : new ResultProblem("Changes to the SFX library can't be saved this session; the log says why");
    }

    private void Change(SfxLibraryContent content)
    {
        Content = content;
        OnChanged.Invoke(Content);
        _ = store.SaveAsync(Content);
    }

    private void LogSeeded(SfxLibraryContent stored, SfxLibraryContent seeded)
    {
        foreach (var panel in seeded.Panels.Where(panel => !stored.Panels.Contains(panel)))
        {
            logger.LogInformation("Added example SFX panel '{Name}' ({PanelId})", panel.Name, panel.Id);
        }

        foreach (var sound in seeded.Sounds.Where(sound => !stored.Sounds.Contains(sound)))
        {
            logger.LogInformation("Added example SFX sound '{Name}' ({SoundId})", sound.Name, sound.Id);
        }
    }

    private void WarnAboutBindings(SfxLibraryContent content)
    {
        foreach (var binding in content.Bindings)
        {
            if (Slot(binding.Slot) is not { } slot)
            {
                logger.LogWarning("The SFX library binds slot {SlotId}, which this game doesn't have; it's kept but unused", binding.Slot);
            }
            else if (binding.Sound is { } sound && content.Sounds.All(existing => existing.Id != sound))
            {
                logger.LogWarning("SFX slot '{Slot}' is bound to sound {SoundId}, which isn't in the library; the slot counts as unbound", slot.Name, sound);
            }
        }
    }

    private static string DescribeInputs(LibrarySound sound, PatchPanel panel) =>
        string.Join(",", panel.Patch.Parameters
            .Where(input => sound.Inputs.ContainsKey(input.Id))
            .Select(input => FormattableString.Invariant($"{input.Name}={sound.Inputs[input.Id]}")));
}
