using System.Collections.Immutable;
using System.Text.Json;
using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Sfx.Validation;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Json;

public static class SfxLibraryCodec
{
    public const int CurrentVersion = 1;

    public static byte[] Serialize(SfxLibraryContent content) =>
        JsonSerializer.SerializeToUtf8Bytes(ToDocument(content), SfxLibraryJsonContext.Default.SfxLibraryDocument);

    public static Result<SfxLibraryContent> Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (Decode(bytes).TryPickProblems(out var problems, out var content))
        {
            return problems;
        }

        return SfxLibraryValidation.Validate(content).TryPickProblems(out problems)
            ? problems
            : content;
    }

    public static Result<SfxLibraryContent> Decode(ReadOnlySpan<byte> bytes)
    {
        SfxLibraryDocument? document;
        try
        {
            var reader = new Utf8JsonReader(bytes);
            using (var parsed = JsonDocument.ParseValue(ref reader))
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Object
                    || !parsed.RootElement.TryGetProperty("version", out var versionElement)
                    || versionElement.ValueKind != JsonValueKind.Number
                    || !versionElement.TryGetInt32(out var version))
                {
                    return new ResultProblem("The SFX library has no version number");
                }

                if (version > CurrentVersion)
                {
                    return new NewerSfxLibraryProblem(version);
                }

                if (version < 1)
                {
                    return new ResultProblem("The SFX library is version {0}; versions start at 1", version);
                }
            }

            document = JsonSerializer.Deserialize(bytes, SfxLibraryJsonContext.Default.SfxLibraryDocument);
        }
        catch (JsonException exception)
        {
            return new ResultProblem(exception, "The SFX library can't be read at {0}", exception.Path ?? "$");
        }
        catch (NotSupportedException exception)
        {
            return new ResultProblem(exception, "The SFX library has a node without a type");
        }

        if (document is null)
        {
            return new ResultProblem("The SFX library is empty");
        }

        var repeated = document.Sounds
            .SelectMany(sound => sound.Inputs.GroupBy(input => input.Input).Where(group => group.Count() > 1).Select(group => (sound.Name, Input: group.Key)))
            .Select(repeat => new ResultProblem("Sound '{0}' sets input {1} more than once", repeat.Name, repeat.Input))
            .ToList();
        if (repeated.Count > 0)
        {
            return Result.Failure<SfxLibraryContent>(repeated);
        }

        return FromDocument(document);
    }

    public static SfxLibraryDocument ToDocument(SfxLibraryContent content) => new(
        CurrentVersion,
        [.. content.Panels.Select(ToDocument)],
        [.. content.Sounds.Select(ToDocument)],
        [.. content.Bindings.Select(binding => new BindingDocument(
            binding.Slot.Value.Value,
            binding.Sound?.Value.Value,
            binding.PitchSemitones,
            binding.GainDb))],
        [.. content.SeededPanels.Select(id => id.Value.Value).Order()],
        [.. content.SeededSounds.Select(id => id.Value.Value).Order()]);

    public static PanelDocument ToDocument(PatchPanel panel) => new(
        panel.Id.Value.Value,
        panel.Name,
        [.. panel.Patch.Nodes.Select(ToDocument)],
        [.. panel.Patch.Connections.Select(connection => new ConnectionDocument(connection.From, connection.To, connection.Parameter))],
        [.. panel.Patch.Parameters.Select(input => new InputDocument(input.Id.Value.Value, input.Name, input.Node, input.Parameter))],
        panel.Patch.Output);

    public static SoundDocument ToDocument(LibrarySound sound) => new(
        sound.Id.Value.Value,
        sound.Name,
        sound.Panel.Value.Value,
        sound.Length,
        [.. sound.Inputs.Select(input => new InputValueDocument(input.Key.Value.Value, input.Value)).OrderBy(input => input.Input)]);

    private static NodeDocument ToDocument(PatchNode node) => node switch
    {
        PatchNode.Oscillator n => new OscillatorDocument(n.Name, n.Waveform, n.Frequency),
        PatchNode.Constant n => new ConstantDocument(n.Name, n.Value),
        PatchNode.Noise n => new NoiseDocument(n.Name, n.Seed),
        PatchNode.Filter n => new FilterDocument(n.Name, n.Type, n.Cutoff, n.Q, n.GainDb),
        PatchNode.Distortion n => new DistortionDocument(n.Name, n.Curve, n.Drive),
        PatchNode.Gain n => new GainDocument(n.Name, n.Amount),
        PatchNode.Envelope n => new EnvelopeDocument(n.Name, n.Attack, n.Decay, n.Sustain, n.Release),
        _ => throw new ArgumentOutOfRangeException(nameof(node), node, null),
    };

    private static PatchNode FromDocument(NodeDocument node) => node switch
    {
        OscillatorDocument n => new PatchNode.Oscillator(n.Name, n.Waveform, n.Frequency),
        ConstantDocument n => new PatchNode.Constant(n.Name, n.Value),
        NoiseDocument n => new PatchNode.Noise(n.Name, n.Seed),
        FilterDocument n => new PatchNode.Filter(n.Name, n.FilterType, n.Cutoff, n.Q, n.GainDb),
        DistortionDocument n => new PatchNode.Distortion(n.Name, n.Curve, n.Drive),
        GainDocument n => new PatchNode.Gain(n.Name, n.Amount),
        EnvelopeDocument n => new PatchNode.Envelope(n.Name, n.Attack, n.Decay, n.Sustain, n.Release),
        _ => throw new ArgumentOutOfRangeException(nameof(node), node, null),
    };

    private static SfxLibraryContent FromDocument(SfxLibraryDocument document) => new(
        [.. document.Panels.Select(panel => new PatchPanel(
            Typed<PatchPanel>(panel.Id),
            panel.Name,
            new Patch(
                [.. panel.Nodes.Select(FromDocument)],
                [.. panel.Connections.Select(connection => new PatchConnection(connection.From, connection.To, connection.Parameter))],
                [.. panel.Inputs.Select(input => new PatchParameter(Typed<PatchInput>(input.Id), input.Name, input.Node, input.Parameter))],
                panel.Output)))],
        [.. document.Sounds.Select(sound => new LibrarySound(
            Typed<LibrarySound>(sound.Id),
            sound.Name,
            Typed<PatchPanel>(sound.Panel),
            sound.Length,
            sound.Inputs.ToImmutableDictionary(input => Typed<PatchInput>(input.Input), input => input.Value)))],
        [.. document.Bindings.Select(binding => new SfxBinding(
            Typed<SfxSlot>(binding.Slot),
            binding.Sound is { } sound ? Typed<LibrarySound>(sound) : null,
            binding.PitchSemitones,
            binding.GainDb))],
        [.. document.SeededPanels.Select(Typed<PatchPanel>)],
        [.. document.SeededSounds.Select(Typed<LibrarySound>)]);

    private static Id<T> Typed<T>(Guid guid) => new(new Id(guid));
}
