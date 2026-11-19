using System.Collections.Immutable;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.Audio;
using Olve.Engine3D.Audio.Devices;
using Olve.Engine3D.Audio.Sfx.Json;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Sfx.Slots;
using Olve.Engine3D.Audio.Sfx.Validation;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Engine3D.Storage;
using Olve.Engine3D.Tests.Storage;
using Olve.Results;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Audio;

public class SfxLibraryTests
{
    private static readonly SfxSlot Click = SfxSlot.Named("interface/click", new BusName("interface"), new SfxVariation(1, 2));
    private static readonly SfxSlot Place = SfxSlot.Named("track/place", new BusName("world"), SfxVariation.None);
    private static readonly SfxSlot Delete = SfxSlot.Named("track/delete", new BusName("world"), SfxVariation.None);

    private readonly InMemoryUserStorage _storage = new(new ManualClock(DateTimeOffset.UnixEpoch));

    private static PatchPanel Panel(string name) => new(
        Id.FromName<PatchPanel>($"panel/{name}"),
        name,
        new Patch(
            [new PatchNode.Oscillator("tone", Waveform.Sine, 440), new PatchNode.Gain("volume", 0.5f)],
            [new PatchConnection("tone", "volume")],
            [new PatchParameter(Id.FromName<PatchInput>($"{name}/pitch"), "Pitch", "tone", PatchNode.Oscillator.FrequencyParameter)],
            "volume"));

    private static LibrarySound Sound(string name, PatchPanel panel, float? pitch = null) => new(
        Id.FromName<LibrarySound>($"sound/{name}"),
        name,
        panel.Id,
        TimeSpan.FromSeconds(0.05),
        pitch is { } value ? ImmutableDictionary<Id<PatchInput>, float>.Empty.Add(panel.Patch.Parameters[0].Id, value) : []);

    private static SfxLibraryContent Content(
        IEnumerable<PatchPanel>? panels = null, IEnumerable<LibrarySound>? sounds = null, IEnumerable<SfxBinding>? bindings = null) =>
        new([.. panels ?? []], [.. sounds ?? []], [.. bindings ?? []], [], []);

    private static SfxBinding Bind(SfxSlot slot, LibrarySound sound) => new(slot.Id, sound.Id, null, null);

    private async Task Store(SfxLibraryContent content) => await Store(SfxLibraryCodec.Serialize(content));

    private async Task Store(byte[] bytes) =>
        await Assert.That(await _storage.WriteAsync(SfxLibraryStore.Key, bytes, CancellationToken.None)).Succeeded();

    private async Task<byte[]> Stored() =>
        (await _storage.ReadAsync(SfxLibraryStore.Key, CancellationToken.None)).TryPickProblems(out var problems, out var bytes)
            ? throw new InvalidOperationException(problems.First().ToBriefString())
            : bytes;

    private async Task<SfxLibraryManager> Load(SfxExamples? examples = null, IUserStorage? storage = null)
    {
        var library = Manager(storage ?? _storage, new SfxLibraryOptions([Click, Place, Delete], examples ?? new SfxExamples([], [])));
        await library.LoadAsync(CancellationToken.None);
        return library;
    }

    private static SfxLibraryManager Manager(IUserStorage storage, SfxLibraryOptions options) => new(
        new SfxLibraryStore(storage, NullLogger<SfxLibraryStore>.Instance), options, NullLogger<SfxLibraryManager>.Instance);

    private static List<string> Messages(ResultProblemCollection problems) => [.. problems.Select(problem => problem.FormattedMessage)];

    public static IEnumerable<Func<PatchNode>> EveryNodeType() =>
    [
        () => new PatchNode.Oscillator("node", Waveform.Triangle, 123.5f),
        () => new PatchNode.Constant("node", 0.25f),
        () => new PatchNode.Noise("node", 42),
        () => new PatchNode.Filter("node", FilterType.Highshelf, 900, 0.7f, -6),
        () => new PatchNode.Distortion("node", DistortionCurve.Fold, 3),
        () => new PatchNode.Gain("node", 0.75f),
        () => new PatchNode.Envelope("node", TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(80), 0.3f, TimeSpan.FromMilliseconds(20)),
    ];

    [Test]
    [MethodDataSource(nameof(EveryNodeType))]
    public async Task A_Node_Comes_Back_As_It_Was_Written(PatchNode node)
    {
        var panel = new PatchPanel(Id.New<PatchPanel>(), "panel", new Patch([node], [], [], node.Name));

        var read = SfxLibraryCodec.Deserialize(SfxLibraryCodec.Serialize(Content([panel])));

        await Assert.That(read).Succeeded();
        read.TryPickProblems(out _, out var content);
        await Assert.That(content!.Panels.Single().Patch.Nodes.Single()).IsEqualTo(node);
    }

    [Test]
    public async Task Every_Node_Type_Has_A_Row_Above()
    {
        var covered = EveryNodeType().Select(row => row().GetType()).ToHashSet();
        var missing = typeof(PatchNode).GetNestedTypes()
            .Where(type => type.IsSubclassOf(typeof(PatchNode)) && !covered.Contains(type))
            .Select(type => type.Name);

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task A_Library_Comes_Back_As_It_Was_Written()
    {
        var panel = Panel("beep");
        var sound = Sound("high beep", panel, 880);
        var content = Content([panel], [sound], [Bind(Click, sound), new SfxBinding(Place.Id, null, 3, null)]) with
        {
            SeededPanels = [panel.Id],
            SeededSounds = [sound.Id],
        };

        var bytes = SfxLibraryCodec.Serialize(content);
        var read = SfxLibraryCodec.Deserialize(bytes);

        await Assert.That(read).Succeeded();
        read.TryPickProblems(out _, out var back);
        await Assert.That(SfxLibraryCodec.Serialize(back!)).IsEquivalentTo(bytes);
        await Assert.That(back!.Sounds.Single().Inputs).IsEquivalentTo(sound.Inputs);
        await Assert.That(back.Bindings).IsEquivalentTo(content.Bindings);
        await Assert.That(back.SeededPanels).IsEquivalentTo(content.SeededPanels);
        await Assert.That(back.SeededSounds).IsEquivalentTo(content.SeededSounds);
    }

    public static IEnumerable<Func<(string Case, string Json, string[] Messages)>> BrokenLibraries()
    {
        var panel = Panel("beep");
        var sound = Sound("beep", panel, 880);
        var valid = Encoding.UTF8.GetString(SfxLibraryCodec.Serialize(Content([panel], [sound])));
        string Json(SfxLibraryContent content) => Encoding.UTF8.GetString(SfxLibraryCodec.Serialize(content));
        var unknownInput = Id.FromName<PatchInput>("nowhere");
        var brokenPanel = panel with { Patch = panel.Patch with { Connections = [new PatchConnection("tone", "nowhere")] } };

        return
        [
            () => ("not JSON", "beep", ["The SFX library can't be read at $"]),
            () => ("no version", "{}", ["The SFX library has no version number"]),
            () => ("version isn't a number", valid.Replace("\"version\": 1", "\"version\": \"1\""), ["The SFX library has no version number"]),
            () => ("node without a type", valid.Replace("\"type\": \"gain\",", ""), ["The SFX library has a node without a type"]),
            () => ("newer version", valid.Replace("\"version\": 1", "\"version\": 2"), ["The SFX library is version 2; this game reads versions 1 to 1"]),
            () => ("unknown node type", valid.Replace("\"type\": \"gain\"", "\"type\": \"theremin\""), ["The SFX library can't be read at $.panels[0].nodes[1]"]),
            () => ("missing panel", Json(Content([], [sound])), [$"Sound 'beep' is made from panel {panel.Id}, which isn't in the library"]),
            () => ("unknown input", Json(Content([panel], [sound with { Inputs = sound.Inputs.Add(unknownInput, 1) }])),
                [$"Sound 'beep' sets inputs that panel 'beep' doesn't have: {unknownInput}"]),
            () => ("negative length", Json(Content([panel], [sound with { Length = TimeSpan.FromSeconds(-1) }])), ["Sound 'beep' can't last a negative time (-00:00:01)"]),
            () => ("broken panel", Json(Content([brokenPanel])), ["Panel 'beep' is broken: A connection goes to 'nowhere', which isn't in the patch"]),
            () => ("repeated panel", Json(Content([panel, panel])), [$"Two panels have the id {panel.Id}"]),
            () => ("repeated sound", Json(Content([panel], [sound, sound])), [$"Two sounds have the id {sound.Id}"]),
            () => ("repeated binding", Json(Content([panel], [sound], [Bind(Click, sound), Bind(Click, sound)])), [$"Slot {Click.Id} is bound more than once"]),
        ];
    }

    [Test]
    [MethodDataSource(nameof(BrokenLibraries))]
    public async Task A_Broken_Library_Is_Rejected_With_What_Is_Wrong(string @case, string json, string[] messages)
    {
        var read = SfxLibraryCodec.Deserialize(Encoding.UTF8.GetBytes(json));

        await Assert.That(read.TryPickProblems(out var problems)).IsTrue();
        await Assert.That(string.Join("\n", Messages(problems!))).IsEqualTo(string.Join("\n", messages));
    }

    [Test]
    public async Task Deleting_A_Panel_Deletes_Its_Sounds_And_Unbinds_Their_Slots()
    {
        var beep = Panel("beep");
        var boop = Panel("boop");
        var high = Sound("high", beep, 880);
        var low = Sound("low", beep, 220);
        var other = Sound("other", boop);
        var content = Content([beep, boop], [high, low, other], [
            Bind(Click, high),
            Bind(Place, high) with { PitchSemitones = 2 },
            Bind(Delete, other),
        ]);

        var planned = SfxDeletionPlanner.ForPanel(content, beep.Id);
        await Assert.That(planned).Succeeded();
        planned.TryPickProblems(out _, out var deletion);
        await Assert.That(deletion!.Sounds.Select(sound => sound.Name)).IsEquivalentTo(["high", "low"]);
        await Assert.That(deletion.UnboundSlots).IsEquivalentTo([Click.Id, Place.Id]);

        var after = SfxDeletionPlanner.Apply(content, deletion);

        await Assert.That(after.Panels.Select(panel => panel.Name)).IsEquivalentTo(["boop"]);
        await Assert.That(after.Sounds.Select(sound => sound.Name)).IsEquivalentTo(["other"]);
        await Assert.That(SfxSlotResolver.Resolve(Click, after).Sound).IsNull();
        await Assert.That(SfxSlotResolver.Resolve(Place, after))
            .IsEqualTo(new SfxSlotState(Place, null, new SfxVariation(2, 0), PitchOverridden: true, GainOverridden: false));
        await Assert.That(SfxSlotResolver.Resolve(Delete, after).Sound?.Name).IsEqualTo("other");
    }

    [Test]
    public async Task Examples_Are_Seeded_Once_So_Deleted_Ones_Stay_Deleted_And_New_Ones_Arrive()
    {
        var beep = Panel("example/beep");
        var boop = Panel("example/boop");
        var beepSound = Sound("example/beep", beep);
        var boopSound = Sound("example/boop", boop);
        var lateBeepSound = Sound("example/beep-late", beep);

        var first = await Load(new SfxExamples([beep], [beepSound]));
        await Assert.That(first.SlotState(Click).Sound).IsNull();
        await Assert.That(first.PlanPanelDeletion(beep.Id).TryPickProblems(out _, out var deletion)).IsFalse();
        await Assert.That(first.Delete(deletion!)).Succeeded();
        await Assert.That(await first.Flush()).Succeeded();

        var second = await Load(new SfxExamples([beep, boop], [beepSound, boopSound, lateBeepSound]));

        await Assert.That(second.Content.Panels.Select(panel => panel.Name)).IsEquivalentTo(["example/boop"]);
        await Assert.That(second.Content.Sounds.Select(sound => sound.Name)).IsEquivalentTo(["example/boop"]);
        await Assert.That(second.Content.SeededPanels).IsEquivalentTo([beep.Id, boop.Id]);
        await Assert.That(second.Content.SeededSounds).IsEquivalentTo([beepSound.Id, boopSound.Id, lateBeepSound.Id]);
        var third = await Load(new SfxExamples([beep, boop], [beepSound, boopSound, lateBeepSound]));
        await Assert.That(SfxLibraryCodec.Serialize(third.Content)).IsEquivalentTo(SfxLibraryCodec.Serialize(second.Content));
    }

    [Test]
    public async Task A_Broken_Stored_Library_Is_Kept_Aside_And_A_New_One_Starts()
    {
        var broken = Encoding.UTF8.GetBytes("{\"version\": 1, \"panels\": [");
        await Store(broken);
        var beep = Panel("example/beep");

        var library = await Load(new SfxExamples([beep], []));

        await Assert.That(library.Content.Panels.Single().Name).IsEqualTo("example/beep");
        var entries = (await _storage.ListAsync(SfxLibraryStore.Key.Area, CancellationToken.None)).TryPickProblems(out _, out var listed) ? [] : listed;
        var kept = entries.Single(entry => entry.Key != SfxLibraryStore.Key);
        var keptBytes = (await _storage.ReadAsync(kept.Key, CancellationToken.None)).TryPickProblems(out _, out var bytes) ? [] : bytes;
        await Assert.That(keptBytes).IsEquivalentTo(broken);
        await Assert.That(SfxLibraryCodec.Deserialize(await Stored())).Succeeded();
    }

    [Test]
    public async Task A_Library_From_A_Newer_Game_Is_Left_As_It_Is()
    {
        var newer = Encoding.UTF8.GetBytes("{\"version\": 2}");
        await Store(newer);

        var library = await Load(new SfxExamples([Panel("example/beep")], []));

        await Assert.That(library.Content.Panels.Single().Name).IsEqualTo("example/beep");
        await Assert.That(Messages(Problems(library.Unbind(Click.Id))))
            .IsEquivalentTo(["Changes to the SFX library can't be saved this session; the log says why"]);
        await Assert.That(await Stored()).IsEquivalentTo(newer);
        await Assert.That((await _storage.ListAsync(SfxLibraryStore.Key.Area, CancellationToken.None)).TryPickProblems(out _, out var entries) ? 0 : entries.Count)
            .IsEqualTo(1);
    }

    [Test]
    public async Task Nothing_Changes_Until_The_Library_Has_Loaded()
    {
        var library = Manager(_storage, new SfxLibraryOptions([Click], new SfxExamples([], [])));

        await Assert.That(Messages(Problems(library.Unbind(Click.Id)))).IsEquivalentTo(["The SFX library is still loading"]);
        await Assert.That(await _storage.ReadAsync(SfxLibraryStore.Key, CancellationToken.None)).Failed();
    }

    [Test]
    public async Task Bindings_To_Missing_Sounds_And_Unknown_Slots_Count_As_Unbound_And_Are_Kept()
    {
        var beep = Panel("beep");
        var sound = Sound("beep", beep);
        var gone = new SfxBinding(Click.Id, Id.FromName<LibrarySound>("gone"), null, null);
        var unknown = new SfxBinding(Id.FromName<SfxSlot>("train/horn"), sound.Id, null, null);
        await Store(Content([beep], [sound], [gone, unknown]));

        var library = await Load();
        await Assert.That(library.SlotState(Click).Sound).IsNull();

        await Assert.That(library.Bind(Place.Id, sound.Id, null, null)).Succeeded();
        await Assert.That(await library.Flush()).Succeeded();
        await Assert.That(SfxLibraryCodec.Deserialize(await Stored()).TryPickProblems(out _, out var stored)).IsFalse();
        await Assert.That(stored!.Bindings).Contains(gone);
        await Assert.That(stored.Bindings).Contains(unknown);
    }

    [Test]
    public async Task Binding_Checks_The_Slot_And_The_Sound()
    {
        var beep = Panel("beep");
        var sound = Sound("beep", beep);
        await Store(Content([beep], [sound]));
        var library = await Load();
        var horn = Id.FromName<SfxSlot>("train/horn");
        var gone = Id.FromName<LibrarySound>("gone");

        await Assert.That(Messages(Problems(library.Bind(horn, sound.Id, null, null)))).IsEquivalentTo([$"There's no SFX slot {horn}"]);
        await Assert.That(Messages(Problems(library.Bind(Click.Id, gone, null, null)))).IsEquivalentTo([$"There's no sound {gone} in the SFX library"]);
        await Assert.That(Messages(Problems(library.Unbind(Click.Id)))).IsEquivalentTo(["SFX slot 'interface/click' isn't bound"]);
    }

    [Test]
    public async Task A_Variation_Override_Sets_Only_The_Values_Given()
    {
        var beep = Panel("beep");
        var sound = Sound("beep", beep);
        await Store(Content([beep], [sound]));
        var library = await Load();

        await Assert.That(library.Bind(Click.Id, sound.Id, 5, null)).Succeeded();
        await Assert.That(library.SlotState(Click)).IsEqualTo(new SfxSlotState(Click, sound, new SfxVariation(5, 2), PitchOverridden: true, GainOverridden: false));

        await Assert.That(library.Bind(Click.Id, sound.Id, null, 3)).Succeeded();
        await Assert.That(library.SlotState(Click)).IsEqualTo(new SfxSlotState(Click, sound, new SfxVariation(5, 3), PitchOverridden: true, GainOverridden: true));

        await Assert.That(Messages(Problems(library.Bind(Click.Id, sound.Id, -1, null)))).IsEquivalentTo(["A variation can't be negative"]);
    }

    private static ResultProblemCollection Problems(Result result) =>
        result.TryPickProblems(out var problems) ? problems : throw new InvalidOperationException("Expected a failure");

    [Test]
    public async Task An_Import_Merges_By_Id()
    {
        var beep = Panel("beep");
        var boop = Panel("boop");
        var high = Sound("high", beep, 880);
        var low = Sound("low", beep, 220);
        var content = Content([beep], [high], [Bind(Click, high)]);
        var file = SfxLibraryCodec.Serialize(Content(
            [boop],
            [high with { Name = "higher", Inputs = high.Inputs.SetItem(beep.Patch.Parameters[0].Id, 1760) }, low, Sound("bop", boop)],
            [Bind(Place, low)]));

        var planned = SfxLibraryImport.Plan(content, file);

        await Assert.That(planned).Succeeded();
        planned.TryPickProblems(out _, out var plan);
        await Assert.That(plan!.Summary).IsEqualTo(new SfxImport(PanelsAdded: 1, PanelsReplaced: 0, SoundsAdded: 2, SoundsReplaced: 1, BindingsAdded: 1, BindingsReplaced: 0));
        await Assert.That(plan.Merged.Sounds.Select(sound => sound.Name)).IsEquivalentTo(["higher", "low", "bop"]);
        await Assert.That(SfxSlotResolver.Resolve(Click, plan.Merged).Sound?.Name).IsEqualTo("higher");
        await Assert.That(SfxSlotResolver.Resolve(Place, plan.Merged).Sound?.Name).IsEqualTo("low");
    }

    [Test]
    public async Task A_Bad_Import_Changes_Nothing()
    {
        var beep = Panel("beep");
        var sound = Sound("beep", beep);
        await Store(Content([beep], [sound]));
        var library = await Load();
        var before = await Stored();
        var orphan = Sound("orphan", Panel("elsewhere"));

        var imported = library.Import(SfxLibraryCodec.Serialize(Content([], [orphan])));

        await Assert.That(imported.TryPickProblems(out var problems)).IsTrue();
        await Assert.That(Messages(problems!)).IsEquivalentTo([
            "The SFX library to import doesn't fit with this one",
            $"Sound 'orphan' is made from panel {orphan.Panel}, which isn't in the library",
        ]);

        var repeated = library.Import(SfxLibraryCodec.Serialize(Content([beep], [sound, sound])));

        await Assert.That(repeated.TryPickProblems(out problems)).IsTrue();
        await Assert.That(Messages(problems!)).IsEquivalentTo([
            "The SFX library to import repeats ids",
            $"Two sounds have the id {sound.Id}",
        ]);
        await Assert.That(library.Content.Sounds.Single().Name).IsEqualTo("beep");
        await Assert.That(await Stored()).IsEquivalentTo(before);
    }

    [Test]
    public async Task Writes_Land_In_The_Order_The_Changes_Were_Made()
    {
        var beep = Panel("beep");
        var high = Sound("high", beep, 880);
        var low = Sound("low", beep, 220);
        await Store(Content([beep], [high, low]));
        var storage = new GatedStorage(_storage);
        var library = await Load(storage: storage);

        await Assert.That(library.Bind(Click.Id, high.Id, null, null)).Succeeded();
        await Assert.That(library.Bind(Click.Id, low.Id, null, null)).Succeeded();

        var firstWrite = await storage.NextWrite();
        await Assert.That(storage.HasWriteWaiting).IsFalse();
        firstWrite.Gate.SetResult();
        var secondWrite = await storage.NextWrite();
        secondWrite.Gate.SetResult();
        await Assert.That(await library.Flush()).Succeeded();

        await Assert.That(BoundTo(firstWrite.Bytes)).IsEqualTo(high.Id);
        await Assert.That(BoundTo(secondWrite.Bytes)).IsEqualTo(low.Id);
        await Assert.That(BoundTo(await Stored())).IsEqualTo(low.Id);
    }

    private static string Problem<T>(Result<T> result) =>
        result.TryPickProblems(out var problems) ? string.Join(": ", problems.Select(problem => problem.ToBriefString())) : "succeeded";

    private static Id<LibrarySound>? BoundTo(byte[] bytes) =>
        SfxLibraryCodec.Deserialize(bytes).TryPickProblems(out _, out var content) ? null : content.Bindings.Single().Sound;

    [Test]
    public async Task Only_Bound_Sounds_Are_Rendered_And_A_Rebind_Renders_Again()
    {
        var beep = Panel("beep");
        var high = Sound("high", beep, 880);
        var low = Sound("low", beep, 220);
        await Store(Content([beep], [high, low], [Bind(Click, high)]));
        var library = await Load();
        var audio = new SoftwareAudioBackend(new FakeAudioStreamBackend());
        var sounds = SoundRendering.CreateSoundManager(audio);
        var devices = new AudioDeviceManager(audio, NullLogger<AudioDeviceManager>.Instance);
        _ = devices.Open(new AudioDeviceSelection.Default(), new AudioOutputSettings());
        var mixer = new AudioMixer(devices, sounds, NullLogger<AudioMixer>.Instance);
        var segment = new AudioSegment(mixer, sounds, NullLogger<AudioSegment>.Instance);
        var slots = new SfxSlotService(library, sounds, mixer, segment, NullLogger<SfxSlotService>.Instance);
        var previews = new SfxPreviewService(library, sounds, mixer, segment);
        _ = slots.RenderBoundSounds(library.Content);
        library.OnChanged.Subscribe(content => _ = slots.RenderBoundSounds(content));
        library.OnChanged.Subscribe(content => _ = previews.DropStalePreviews(content));

        await Assert.That(sounds.RenderingCount).IsEqualTo(1);
        await Assert.That(Problem(slots.Play(Click.Id))).IsEqualTo("SFX slot 'interface/click' is bound to sound 'high', which is still rendering");
        sounds.RenderAll();
        await Assert.That(sounds.Count).IsEqualTo(1);
        await Assert.That(Problem(slots.Play(Click.Id))).IsEqualTo("SFX slot 'interface/click' can't play: There's no audio bus 'interface'");
        var bus = mixer.AddBus(new BusName("interface"));
        _ = mixer.AddBus(new BusName("world"));
        await Assert.That(slots.Play(Click.Id)).SucceededAndValue(voice => voice.IsNotNull());
        await Assert.That(mixer.Voices.Single().Bus).IsEqualTo(bus);
        await Assert.That(slots.Play(Place.Id)).SucceededAndValue(voice => voice.IsNull());

        await Assert.That(Problem(previews.Preview(low.Id, bus, new VoiceSettings()))).IsEqualTo("Sound 'low' is still rendering; preview it again once it has");
        sounds.RenderAll();
        await Assert.That(previews.Preview(low.Id, bus, new VoiceSettings())).Succeeded();
        await Assert.That(sounds.Count).IsEqualTo(2);

        await Assert.That(library.Bind(Click.Id, low.Id, null, null)).Succeeded();
        await Assert.That((sounds.Count, sounds.RenderingCount)).IsEqualTo((1, 1));
        await Assert.That(slots.RenderedSlots).IsEqualTo(1);

        await Assert.That(library.PlanPanelDeletion(beep.Id).TryPickProblems(out _, out var deletion)).IsFalse();
        await Assert.That(library.Delete(deletion!)).Succeeded();
        await Assert.That(sounds.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(0.0, 0.0, 1f, 1f)]
    [Arguments(1.0, 1.0, 1.122462f, 1.258925f)]
    [Arguments(-1.0, -1.0, 0.890899f, 0.794328f)]
    public async Task A_Variation_Moves_Pitch_By_Semitones_And_Gain_By_Decibels(double pitchRoll, double gainRoll, float pitch, float gain)
    {
        var settings = SfxRendering.Vary(new SfxVariation(PitchSemitones: 2, GainDb: 2), pitchRoll, gainRoll);

        await Assert.That(settings.Pitch).IsEqualTo(pitch).Within(1e-5f);
        await Assert.That(settings.Gain).IsEqualTo(gain).Within(1e-5f);
    }

    private sealed class GatedStorage(IUserStorage inner) : IUserStorage
    {
        private readonly Channel<(byte[] Bytes, TaskCompletionSource Gate)> _writes = Channel.CreateUnbounded<(byte[], TaskCompletionSource)>();

        public bool HasWriteWaiting => _writes.Reader.TryPeek(out _);

        public ValueTask<(byte[] Bytes, TaskCompletionSource Gate)> NextWrite() => _writes.Reader.ReadAsync();

        public ValueTask<Result<IReadOnlyList<UserStorageEntry>>> ListAsync(Id<UserStorageArea> area, CancellationToken cancellationToken) =>
            inner.ListAsync(area, cancellationToken);

        public ValueTask<Result<byte[]>> ReadAsync(UserStorageKey key, CancellationToken cancellationToken) =>
            inner.ReadAsync(key, cancellationToken);

        public async ValueTask<Result> WriteAsync(UserStorageKey key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await _writes.Writer.WriteAsync((bytes.ToArray(), gate), cancellationToken);
            await gate.Task;
            return await inner.WriteAsync(key, bytes, cancellationToken);
        }

        public ValueTask<DeletionResult> DeleteAsync(UserStorageKey key, CancellationToken cancellationToken) =>
            inner.DeleteAsync(key, cancellationToken);
    }
}
