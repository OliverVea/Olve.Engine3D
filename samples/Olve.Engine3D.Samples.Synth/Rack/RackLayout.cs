using System.Numerics;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Playing;

namespace Olve.Engine3D.Samples.Synth.Rack;

/// <summary>An axis-aligned rectangle in design units.</summary>
public readonly record struct Box(Vector2 Min, Vector2 Size)
{
    public Vector2 Max => Min + Size;

    public Vector2 Centre => Min + Size / 2;

    public bool Contains(Vector2 point) => point.X >= Min.X && point.Y >= Min.Y && point.X < Max.X && point.Y < Max.Y;
}

/// <summary>A knob, or with a <paramref name="Slot"/> a fader: its cap rides the slot, bottom to top as its value rises.</summary>
public sealed record KnobLayout(ModuleId Module, ParameterDefinition Parameter, Vector2 Centre, float Radius, Box? Slot = null)
{
    public bool IsFader => Slot is not null;

    /// <summary>Where its name is printed, if not under the knob: on a card, under its CV jack.</summary>
    public Vector2? Label { get; init; }

    /// <summary>For a push button (<see cref="FaceControl.Toggle"/>), its cap: a press flips its switch, as a click on a stepped knob steps it.</summary>
    public Box? Button { get; init; }

    public bool IsButton => Button is not null;

    /// <summary>Whether it prints its name rather than its setting, as a readout beside it shows that.</summary>
    public bool Named { get; init; }

    /// <summary>The slack round a knob that still turns it, a third of its radius.</summary>
    public float Slack => Radius / 3;

    /// <summary>The area that reaches it: the knob with a little slack round it, anywhere along the fader's slot, or the button's cap with a little round it.</summary>
    public Box Reach => Slot is { } slot
        ? new Box(slot.Min - new Vector2(8, 6), slot.Size + new Vector2(16, 12))
        : Button is { } button
            ? new Box(button.Min - new Vector2(3), button.Size + new Vector2(6))
            : new Box(Centre - new Vector2(Radius + Slack), new Vector2(2 * (Radius + Slack)));

    /// <summary>Whether a point is on the control: the knob with a little slack round it, anywhere along the fader's slot, or on the button.</summary>
    public bool Contains(Vector2 point) => Slot is not null || Button is not null
        ? Reach.Contains(point)
        : Vector2.Distance(Centre, point) <= Radius + Slack;
}

/// <summary>A line of text printed on a card, such as a strip's title.</summary>
public sealed record CaptionLayout(string Text, Vector2 Centre);

public sealed record JackLayout(JackRef Jack, JackDefinition Definition, Vector2 Centre, float Radius)
{
    /// <summary>Its name as printed, if not the jack's own: on a strip, without the strip's title (PITCH on VCO 1).</summary>
    public string? Label { get; init; }

    /// <summary>Where its name is printed, if not over it: on a card, under it.</summary>
    public Vector2? LabelAt { get; init; }

    /// <summary>The slack round a jack that still reaches it, nearly half its radius.</summary>
    public float Slack => Radius * 0.45f;
}

/// <summary>What a button does when it is pressed.</summary>
public enum ButtonAction
{
    OctaveDown,
    OctaveUp,
    PreviousInput,
    NextInput,
}

public sealed record ButtonLayout(ModuleId Module, ButtonAction Action, Box Bounds);

/// <summary>A thumb wheel, turned by dragging along it; the pitch wheel springs back to its middle when let go.</summary>
public sealed record WheelLayout(ModuleId Module, ParameterDefinition Parameter, Box Bounds, bool Springs);

/// <summary>Where a module's panel is: in the rack's rows, on a box of its own on the desk, or in the side case left of the seat.</summary>
public enum PanelPlace
{
    Rack,
    Desk,
    Side,
}

/// <summary>
/// A module's card: its title, numbered when the rack holds more than one of its kind, its group, and where its parts sit.
/// A face's input picker (<see cref="Display"/>) shows the input playing and is pressed to start it, its buttons stepping
/// through the inputs; a level meter stands in <see cref="Meter"/>.
/// </summary>
public sealed record PanelLayout(
    Module Module,
    string Title,
    Box Bounds,
    IReadOnlyList<KnobLayout> Knobs,
    IReadOnlyList<JackLayout> Jacks,
    Box? Display = null,
    PanelPlace Place = PanelPlace.Rack)
{
    public IReadOnlyList<ButtonLayout> Buttons { get; init; } = [];

    public ModuleGroup Group { get; init; } = ModuleGroup.Utility;

    public IReadOnlyList<CaptionLayout> Captions { get; init; } = [];

    public Box? Meter { get; init; }

    /// <summary>The sequencer's step lights, one over each step's gate button, in step order.</summary>
    public IReadOnlyList<Vector2> StepLights { get; init; } = [];

    /// <summary>The quantizer's readout naming its scale.</summary>
    public Box? Readout { get; init; }
}

public sealed record PianoKeyLayout(int Semitone, Box Bounds, bool IsBlack);

/// <summary>
/// The keybed's left cheek, for the keyboard module: its pitch and mod wheels, the octave buttons under the octave's
/// readout, the VOICES selector over its own readout, and its PITCH, GATE and MOD outputs. It is drawn flat, unlike the
/// keys, which are deeper than drawn.
/// </summary>
public sealed record CheekLayout(
    Module Keyboard,
    Box Bounds,
    IReadOnlyList<WheelLayout> Wheels,
    IReadOnlyList<ButtonLayout> Buttons,
    Box Readout,
    IReadOnlyList<KnobLayout> Knobs,
    Box VoicesReadout,
    IReadOnlyList<JackLayout> Jacks);

/// <summary>
/// A scope, the left (index 0) or the right, over a corner of the case and off the panels. Its input jack has a design
/// point of its own, far from the panels, that only a view placing the scope in the room maps to and from.
/// </summary>
public sealed record ScopeLayout(Module Module, int Index, JackLayout In);

/// <summary>What is under a point on the rack.</summary>
public abstract record RackHit
{
    private RackHit()
    {
    }

    public sealed record Knob(KnobLayout Layout) : RackHit;

    public sealed record Jack(JackLayout Layout) : RackHit;

    public sealed record PianoKey(PianoKeyLayout Layout) : RackHit;

    public sealed record Wheel(WheelLayout Layout) : RackHit;

    public sealed record Button(ButtonLayout Layout) : RackHit;

    /// <summary>The line input's display, pressed to start the input or to change to the next one.</summary>
    public sealed record Picker(PanelLayout Panel) : RackHit;
}

/// <summary>
/// Where everything sits, in design units. Each module in the rack is a card laid out from its face
/// (<see cref="ModuleFaces"/>): its strips from the top, each with its inputs on the left, its knobs and faders in the
/// middle with their CV jacks under them, and its outputs on the right in their rings of level lights; an effect takes
/// its input at the top and gives its output at the bottom. The cards stand in columns along the path of the sound:
/// sources and modulation, then the shapers, the effects, and the output and utilities, each zone wrapping into a further
/// column when it grows too tall, its cards growing to fill the column out. A tall speaker panel stands at each end. Under the rows is the keybed: the keyboard's cheek on the left, then the piano's keys. The
/// LIGHTS module has a box of its own on the desk, right of the keybed, and the scopes have no panel. An
/// <see cref="IRackView"/> puts it on the screen.
/// </summary>
public sealed class RackLayout
{
    public const float Margin = 20;
    public const float PanelGap = 10;
    public const float RackTop = 44;
    public const float HintY = 22;
    public const float TitleY = 20;
    public const float KnobRadius = 18;
    public const float JackRadius = 11;
    public const float KnobJackRadius = 9;
    public const float PianoGap = 20;
    public const float WhiteKeyHeight = 104;
    public const float BlackKeyHeight = 62;

    /// <summary>The narrowest a white key gets; with room to spare the keys widen to fill the keybed.</summary>
    public const float MinWhiteKeyWidth = 44;

    /// <summary>A black key's width, as a share of a white key's.</summary>
    public const float BlackKeyShare = 0.64f;

    /// <summary>The keyboard's cheek, left of the keys: its width, and its depth, as deep as the keys really are.</summary>
    public const float CheekWidth = 320;
    public const float CheekDepth = 187;

    /// <summary>The gap from the cheek to the keys, and the narrow cheek right of them.</summary>
    public const float KeyGap = 10;
    public const float RightCheek = 24;

    /// <summary>The narrowest the speaker panels at each end of the rows get; they widen to fill a case the keybed widens.</summary>
    public const float SpeakerWidth = 170;

    /// <summary>The LIGHTS box's panel, and how far right of the keybed it stands.</summary>
    public static readonly Vector2 LightsSize = new(3 * Slot + 2 * PanelPadding, 280);
    public const float DeskGap = 60;

    /// <summary>On a wide screen the piano has four octaves, on a narrow or touch one three.</summary>
    public const int WideOctaves = 4;
    public const int NarrowOctaves = 3;

    /// <summary>The tallest a column of cards gets before its zone wraps into more columns.</summary>
    public const float MaxColumnHeight = 760;

    /// <summary>The width each knob, fader or jack takes along a strip, and a card's padding at each side.</summary>
    public const float Slot = 52;
    public const float PanelPadding = 12;

    /// <summary>A card's title band, its stripe across the top; and the title's height in it.</summary>
    public const float CardTop = 24, CardTitleY = 14;

    /// <summary>On a card: the width a knob, fader or jack takes along a strip, and the size of its knobs and jacks.</summary>
    public const float CardSlot = 40, CardKnobRadius = 13, BigKnobRadius = 18, CardJackRadius = 8, CardCvJackRadius = 7;

    /// <summary>A strip's height: with knobs, with faders; and its title's band when it has one.</summary>
    private const float StripHeight = 78, FaderStripHeight = 128, StripTitle = 12;

    // Heights within a strip, from its top: the knobs and the jacks beside them, the knobs' CV jacks, and a fader's slot
    private const float KnobRow = 24, JackRow = 30;
    private const float KnobJackRow = 52, LabelDrop = 14;
    private const float FaderTop = 6, FaderLength = 78, FaderJackRow = 104;

    /// <summary>The line input's display: its width in slots, with its step buttons either side, and its height.</summary>
    private const float DisplaySlots = 3.8f, DisplayHeight = 44;

    /// <summary>How wide a character of a label under a control is: the dot font's advance of 6 dots of 1.25 units.</summary>
    private const float LabelCharWidth = 7.5f;

    /// <summary>The line input's step buttons beside its display.</summary>
    private static readonly Vector2 StepButtonSize = new(18, 22);

    /// <summary>The line input's step buttons and the keybed's octave buttons.</summary>
    private static readonly Vector2 ButtonSize = new(34, 24);

    // Within the LIGHTS box: each light's caption, its knobs and their CV jacks, from the box's top
    private static readonly float[] LightRows = [44, 164];

    /// <summary>A strip of push buttons alone, with the lights over them: the light's row, the buttons' row, and the strip's height.</summary>
    private const float StepLightRow = 7, ToggleRow = 24, ToggleStripHeight = 40;

    /// <summary>A push button's cap.</summary>
    public static readonly Vector2 ToggleSize = new(22, 15);

    /// <summary>A strip holding only a readout: the readout's height with a little room round it.</summary>
    private const float ReadoutStripHeight = DisplayHeight + 8;

    /// <summary>The modules whose cards go in the side case, left of the seat: the sequencer and its quantizer.</summary>
    public static bool InSideCase(ModuleKind kind) => kind is ModuleKind.Sequencer or ModuleKind.Quantizer;

    /// <summary>How far right of everything else the side case's cards are laid out: they stand in a room of their own, so their design points are kept apart.</summary>
    public const float SideGap = 200;

    /// <summary>The columns' zones, left to right: which groups stand together.</summary>
    private static readonly ModuleGroup[][] Zones =
    [
        [ModuleGroup.Source, ModuleGroup.Modulation],
        [ModuleGroup.Shape],
        [ModuleGroup.Effects],
        [ModuleGroup.Output, ModuleGroup.Utility],
    ];
    private const float LightKnobDrop = 34, LightJackDrop = 90;

    /// <summary>How many scopes the case has room for: one over each top corner.</summary>
    public const int MaxScopes = 2;

    public RackLayout(SynthPatch patch, int octaves = WideOctaves)
    {
        Octaves = octaves;
        var paneled = patch.Modules.Where(m => m.Kind is not (ModuleKind.Scope or ModuleKind.Keyboard or ModuleKind.Lights) && !InSideCase(m.Kind)).ToList();
        var columns = Columns(paneled);
        var rowsWidth = columns.Sum(c => c.Width) + int.Max(0, columns.Count - 1) * PanelGap;
        var rowsHeight = columns.Count == 0 ? 0 : columns.Max(c => c.Height);
        var whites = Enumerable.Range(0, 12 * octaves + 1).Count(s => !KeyMap.IsBlack(s));
        var keybedWidth = CheekWidth + KeyGap + whites * MinWhiteKeyWidth + RightCheek;
        CaseWidth = float.Max(rowsWidth + 2 * (SpeakerWidth + PanelGap), keybedWidth) + 2 * Margin;

        var panels = new List<PanelLayout>();
        var x = (CaseWidth - rowsWidth) / 2;
        foreach (var column in columns)
        {
            // The cards grow to fill their column, each by the same share of what is left
            var y = RackTop;
            var grow = (rowsHeight - column.Height) / column.Cards.Count;
            foreach (var (module, face, height) in column.Cards)
            {
                panels.Add(LayOut(patch, module, face, new Box(new Vector2(x, y), new Vector2(column.Width, height + grow))));
                y += height + grow + PanelGap;
            }

            x += column.Width + PanelGap;
        }

        PianoTop = RackTop + rowsHeight + PianoGap;
        var speakerWidth = (CaseWidth - 2 * Margin - rowsWidth) / 2 - PanelGap;
        Speakers =
        [
            new Box(new Vector2(Margin, RackTop), new Vector2(speakerWidth, rowsHeight)),
            new Box(new Vector2(CaseWidth - Margin - speakerWidth, RackTop), new Vector2(speakerWidth, rowsHeight)),
        ];
        var whiteWidth = (CaseWidth - 2 * Margin - CheekWidth - KeyGap - RightCheek) / whites;
        PianoKeys = LayOutPiano(new Vector2(Margin + CheekWidth + KeyGap, PianoTop), whiteWidth, 12 * octaves + 1);
        Keys = new Box(new Vector2(Margin + CheekWidth + KeyGap, PianoTop), new Vector2(whites * whiteWidth, WhiteKeyHeight));
        if (patch.Modules.FirstOrDefault(m => m.Kind == ModuleKind.Keyboard) is { } keyboard)
        {
            Cheek = LayOutCheek(keyboard, new Box(new Vector2(Margin, PianoTop), new Vector2(CheekWidth, CheekDepth)));
        }

        if (patch.Modules.FirstOrDefault(m => m.Kind == ModuleKind.Lights) is { } lights)
        {
            panels.Add(LayOutLights(lights, new Box(new Vector2(CaseWidth + DeskGap, PianoTop), LightsSize)));
        }

        var size = new Vector2(CaseWidth + DeskGap + LightsSize.X + Margin, PianoTop + float.Max(CheekDepth, LightsSize.Y) + Margin);
        var side = patch.Modules.Where(m => InSideCase(m.Kind)).Select(m => (Module: m, Face: ModuleFaces.Of(m.Definition))).ToList();
        if (side.Count > 0)
        {
            // The side case's cards in a row, the sequencer before its quantizer, each as tall as the tallest
            var height = side.Max(c => CardHeight(c.Face));
            var cardX = size.X + SideGap + Margin;
            foreach (var (module, face) in side)
            {
                var width = CardWidth(module.Definition, face);
                panels.Add(LayOut(patch, module, face, new Box(new Vector2(cardX, RackTop), new Vector2(width, height))) with { Place = PanelPlace.Side });
                cardX += width + PanelGap;
            }

            Side = new Box(new Vector2(size.X + SideGap, RackTop - Margin), new Vector2(cardX - PanelGap + Margin - size.X - SideGap, height + 2 * Margin));
            size = Vector2.Max(size, Side.Value.Max + new Vector2(Margin));
        }

        Panels = panels;
        Size = size;
        Scopes = [.. patch.Modules.Where(m => m.Kind == ModuleKind.Scope).Take(MaxScopes).Select((scope, i) =>
            new ScopeLayout(scope, i, new JackLayout(new JackRef(scope.Id, Modules.In), scope.Definition.FindJack(Modules.In)!, ScopeJackPoint(i), JackRadius)))];
    }

    /// <summary>The design point standing for a scope's input jack: well away from anything on the panels.</summary>
    public static Vector2 ScopeJackPoint(int index) => new(index * 1000, -5000);

    public int Octaves { get; }

    /// <summary>Everything's extent, the LIGHTS box included.</summary>
    public Vector2 Size { get; }

    /// <summary>The case's width: its rows of panels, or its keybed if wider, and the margins.</summary>
    public float CaseWidth { get; }

    /// <summary>The design y of the keybed's top edge, where the panels' rows end.</summary>
    public float PianoTop { get; }

    /// <summary>Every module's panel: those in the rack's rows, and the LIGHTS box's on the desk.</summary>
    public IReadOnlyList<PanelLayout> Panels { get; }

    public IEnumerable<PanelLayout> RackPanels => Panels.Where(p => p.Place == PanelPlace.Rack);

    public PanelLayout? Desk => Panels.FirstOrDefault(p => p.Place == PanelPlace.Desk);

    /// <summary>The side case's face, its cards with a margin round them, if the patch has a sequencer or a quantizer.</summary>
    public Box? Side { get; }

    public IEnumerable<PanelLayout> SidePanels => Panels.Where(p => p.Place == PanelPlace.Side);

    /// <summary>White keys first, then the black keys that sit over them.</summary>
    public IReadOnlyList<PianoKeyLayout> PianoKeys { get; }

    /// <summary>Where the piano's keys are, as drawn.</summary>
    public Box Keys { get; }

    public CheekLayout? Cheek { get; }

    /// <summary>The left speaker's panel, then the right's, at the ends of the rows.</summary>
    public IReadOnlyList<Box> Speakers { get; }

    public IEnumerable<KnobLayout> Knobs => Panels.SelectMany(p => p.Knobs).Concat(Cheek?.Knobs ?? []);

    public IReadOnlyList<ScopeLayout> Scopes { get; }

    public IEnumerable<JackLayout> Jacks => Panels.SelectMany(p => p.Jacks).Concat(Cheek?.Jacks ?? []).Concat(Scopes.Select(s => s.In));

    public IEnumerable<ButtonLayout> Buttons => Panels.SelectMany(p => p.Buttons).Concat(Cheek?.Buttons ?? []);

    public IEnumerable<WheelLayout> Wheels => Cheek?.Wheels ?? [];

    public JackLayout? FindJack(JackRef jack) => Jacks.FirstOrDefault(j => j.Jack == jack);

    /// <summary>The rails between cards stacked in a column, which they screw into: each spans its upper card's width and the gap under it.</summary>
    public IEnumerable<Box> Rails
    {
        get
        {
            var cards = RackPanels.Select(p => p.Bounds).ToList();
            return cards
                .Where(upper => cards.Any(below => MathF.Abs(below.Min.Y - upper.Max.Y - PanelGap) < 0.5f && MathF.Abs(below.Min.X - upper.Min.X) < 0.5f))
                .Select(upper => new Box(new Vector2(upper.Min.X - PanelGap / 2, upper.Max.Y), new Vector2(upper.Size.X + PanelGap, PanelGap)));
        }
    }

    public RackHit? HitTest(Vector2 point)
    {
        // Jacks and knobs get a little slack around them, as they are small
        if (Jacks.FirstOrDefault(j => Vector2.Distance(j.Centre, point) <= j.Radius + j.Slack) is { } jack)
        {
            return new RackHit.Jack(jack);
        }

        if (Knobs.FirstOrDefault(k => k.Contains(point)) is { } knob)
        {
            return new RackHit.Knob(knob);
        }

        if (Buttons.FirstOrDefault(b => b.Bounds.Contains(point)) is { } button)
        {
            return new RackHit.Button(button);
        }

        if (Wheels.FirstOrDefault(w => w.Bounds.Contains(point)) is { } wheel)
        {
            return new RackHit.Wheel(wheel);
        }

        if (Panels.FirstOrDefault(p => p.Display is { } picker && picker.Contains(point)) is { } panel)
        {
            return new RackHit.Picker(panel);
        }

        // Black keys lie over the white ones, so they're tested first
        return PianoKeys.Reverse().FirstOrDefault(k => k.Bounds.Contains(point)) is { } key ? new RackHit.PianoKey(key) : null;
    }

    private sealed record Column(List<(Module Module, ModuleFace Face, float Height)> Cards, float Width, float Height);


    // The cards in their zones' columns: a zone too tall for one column is split into as few as fit, of about the same height
    private static List<Column> Columns(List<Module> modules)
    {
        var columns = new List<Column>();
        foreach (var zone in Zones)
        {
            var cards = modules.Select(m => (Module: m, Face: ModuleFaces.Of(m.Definition)))
                .Where(c => zone.Contains(c.Face.Group))
                .Select(c => (c.Module, c.Face, Height: CardHeight(c.Face)))
                .ToList();
            if (cards.Count == 0)
            {
                continue;
            }

            var heights = cards.Select(c => c.Height).ToList();
            foreach (var (first, count) in Split(heights))
            {
                var column = cards.GetRange(first, count);
                columns.Add(new Column(column, column.Max(c => CardWidth(c.Module.Definition, c.Face)), Stacked(heights, first, count)));
            }
        }

        return columns;
    }

    // The cards in as few columns as keep each under the tallest, each split where the tallest of them is least
    private static List<(int First, int Count)> Split(List<float> heights)
    {
        var whole = Stacked(heights, 0, heights.Count);
        var count = int.Clamp((int)MathF.Ceiling(whole / MaxColumnHeight), 1, heights.Count);
        return Best(0, count) ?? [(0, heights.Count)];

        List<(int First, int Count)>? Best(int first, int columns)
        {
            if (columns == 1)
            {
                return [(first, heights.Count - first)];
            }

            List<(int, int)>? best = null;
            var tallest = float.MaxValue;
            for (var end = first + 1; end <= heights.Count - columns + 1; end++)
            {
                if (Best(end, columns - 1) is not { } rest)
                {
                    continue;
                }

                var candidate = new List<(int First, int Count)> { (first, end - first) };
                candidate.AddRange(rest);
                var max = candidate.Max(c => Stacked(heights, c.First, c.Count));
                if (max < tallest)
                {
                    tallest = max;
                    best = candidate;
                }
            }

            return best;
        }
    }

    private static float Stacked(List<float> heights, int first, int count) => heights.Skip(first).Take(count).Sum() + (count - 1) * PanelGap;

    /// <summary>A card is as wide as its widest strip: its inputs, controls, readout and outputs a slot each.</summary>
    public static float CardWidth(ModuleDefinition definition, ModuleFace face) => face.Strips.Max(strip =>
        (strip.Inputs.Length + strip.Outputs.Length) * CardSlot + ControlsWidth(strip, definition)) + 2 * PanelPadding;

    private static float ControlsWidth(FaceStrip strip, ModuleDefinition definition) =>
        strip.Controls.Sum(c => ControlWidth(c, definition))
        + strip.Display switch
        {
            FaceDisplay.InputPicker or FaceDisplay.ScaleReadout => DisplaySlots * CardSlot,
            FaceDisplay.LevelMeter => CardSlot * 0.7f,
            _ => 0,
        };

    // A control's slot: wider for a big knob, and for a name too long to fit under it
    private static float ControlWidth(FaceControl control, ModuleDefinition definition)
    {
        var width = CardSlot + (control is FaceControl.Knob { Big: true } ? (BigKnobRadius - CardKnobRadius) * 2 : 0);
        var caption = definition.FindParameter(control.Parameter)?.Caption ?? "";
        return float.Max(width, caption.Length * LabelCharWidth + 6);
    }

    public static float CardHeight(ModuleFace face) => CardTop + face.Strips.Sum(StripTall) + 2;

    // A strip of push buttons alone, which have no CV jacks under them there, and a strip with only a readout are short
    private static float StripTall(FaceStrip strip) => (strip.Title is null ? 0 : StripTitle) + strip switch
    {
        { Controls.Length: 0, Display: FaceDisplay.ScaleReadout } => ReadoutStripHeight,
        _ when strip.Controls.Any(c => c is FaceControl.Fader) => FaderStripHeight,
        _ when strip.Controls.Length > 0 && strip.Controls.All(c => c is FaceControl.Toggle) => ToggleStripHeight,
        _ => StripHeight,
    };

    // A jack a card's face names: none for "", which leaves its slot empty; a name the module doesn't have is a mistake in the face
    private static JackDefinition? FaceJack(ModuleDefinition definition, string name) => name.Length == 0
        ? null
        : definition.FindJack(name) ?? throw new InvalidOperationException($"The {definition.Title} card's face names no jack {name}");

    private static PanelLayout LayOut(SynthPatch patch, Module module, ModuleFace face, Box bounds)
    {
        var definition = module.Definition;
        var knobs = new List<KnobLayout>();
        var jacks = new List<JackLayout>();
        var buttons = new List<ButtonLayout>();
        var captions = new List<CaptionLayout>();
        var stepLights = new List<Vector2>();
        Box? display = null, meter = null, readout = null;
        // An effect takes its input at the top and gives its output at the bottom, as the signal runs down the column
        var diagonal = face.Group == ModuleGroup.Effects;
        var top = bounds.Min.Y + CardTop;
        // A card grown taller than it needs spreads its strips out, each in the middle of its share
        var spare = (bounds.Size.Y - CardHeight(face)) / face.Strips.Length;
        foreach (var strip in face.Strips)
        {
            top += spare / 2;
            if (strip.Title is { } stripTitle)
            {
                captions.Add(new CaptionLayout(stripTitle, new Vector2(bounds.Min.X + PanelPadding, top + StripTitle / 2)));
                top += StripTitle;
            }

            var height = StripTall(strip) - (strip.Title is null ? 0 : StripTitle);
            var x = bounds.Min.X + PanelPadding;
            foreach (var name in strip.Inputs)
            {
                if (FaceJack(definition, name) is { } jack)
                {
                    var at = new Vector2(x + CardSlot / 2, top + (diagonal ? JackRow - 10 : JackRow));
                    jacks.Add(new JackLayout(new JackRef(module.Id, name), jack, at, CardJackRadius)
                    {
                        Label = StripLabel(strip, name), LabelAt = at + new Vector2(0, CardJackRadius + 8),
                    });
                }

                x += CardSlot;
            }

            // The controls and the readout are centred between the inputs and the outputs
            var right = bounds.Max.X - PanelPadding - strip.Outputs.Length * CardSlot;
            x += (right - x - ControlsWidth(strip, definition)) / 2;
            foreach (var control in strip.Controls)
            {
                var parameter = definition.FindParameter(control.Parameter)
                    ?? throw new InvalidOperationException($"The {definition.Title} card's face names no knob {control.Parameter}");

                var big = control is FaceControl.Knob { Big: true };
                var width = ControlWidth(control, definition);
                var centreX = x + width / 2;
                float jackY;
                if (control is FaceControl.Toggle && StripTall(strip) - (strip.Title is null ? 0 : StripTitle) == ToggleStripHeight)
                {
                    // A row of buttons: each under its light, its CV jack, if it has one, close under it
                    var cap = new Box(new Vector2(centreX, top + ToggleRow) - ToggleSize / 2, ToggleSize);
                    jackY = cap.Max.Y + CardCvJackRadius + 2;
                    knobs.Add(new KnobLayout(module.Id, parameter, cap.Centre, ToggleSize.Y / 2) { Button = cap });
                    if (strip.Display == FaceDisplay.StepLights)
                    {
                        stepLights.Add(new Vector2(centreX, top + StepLightRow));
                    }
                }
                else if (control is FaceControl.Toggle)
                {
                    // Among knobs: where a knob would be, its CV jack and name under it as a knob's
                    var cap = new Box(new Vector2(centreX, top + KnobRow) - ToggleSize / 2, ToggleSize);
                    jackY = top + KnobJackRow;
                    knobs.Add(new KnobLayout(module.Id, parameter, cap.Centre, ToggleSize.Y / 2) { Button = cap, Label = new Vector2(centreX, jackY + LabelDrop) });
                }
                else if (control is FaceControl.Fader)
                {
                    var slot = new Box(new Vector2(centreX - 3.5f, top + FaderTop), new Vector2(7, FaderLength));
                    jackY = top + FaderJackRow;
                    knobs.Add(new KnobLayout(module.Id, parameter, slot.Centre, 8, slot) { Label = new Vector2(centreX, jackY + LabelDrop) });
                }
                else
                {
                    jackY = top + KnobJackRow + (big ? 10 : 0);
                    knobs.Add(new KnobLayout(module.Id, parameter, new Vector2(centreX, top + KnobRow + (big ? 2 : 0)), big ? BigKnobRadius : CardKnobRadius)
                    {
                        Label = new Vector2(centreX, jackY + LabelDrop),
                        Named = face.Strips.Any(s => s.Display == FaceDisplay.ScaleReadout),
                    });
                }

                if (definition.FindJack(parameter.CvJack) is { } cv)
                {
                    jacks.Add(new JackLayout(new JackRef(module.Id, parameter.CvJack), cv, new Vector2(centreX, jackY), CardCvJackRadius));
                }

                x += width;
            }

            switch (strip.Display)
            {
                case FaceDisplay.InputPicker:
                {
                    // The display between its step buttons
                    var box = new Box(new Vector2(x + 6 + StepButtonSize.X + 4, top + 6), new Vector2(DisplaySlots * CardSlot - 12 - 2 * (StepButtonSize.X + 4), DisplayHeight));
                    display = box;
                    var buttonsY = box.Centre.Y - StepButtonSize.Y / 2;
                    buttons.Add(new ButtonLayout(module.Id, ButtonAction.PreviousInput, new Box(new Vector2(box.Min.X - 4 - StepButtonSize.X, buttonsY), StepButtonSize)));
                    buttons.Add(new ButtonLayout(module.Id, ButtonAction.NextInput, new Box(new Vector2(box.Max.X + 4, buttonsY), StepButtonSize)));
                    break;
                }
                case FaceDisplay.LevelMeter:
                    meter = new Box(new Vector2(x + CardSlot * 0.35f - 7, top + 6), new Vector2(14, height - 22));
                    break;
                case FaceDisplay.ScaleReadout:
                    readout = new Box(new Vector2(x + 6, top + (height - DisplayHeight) / 2), new Vector2(DisplaySlots * CardSlot - 12, DisplayHeight));
                    break;
            }

            var outX = bounds.Max.X - PanelPadding - strip.Outputs.Length * CardSlot;
            foreach (var name in strip.Outputs)
            {
                if (FaceJack(definition, name) is { } jack)
                {
                    var at = new Vector2(outX + CardSlot / 2, top + (diagonal ? height - 16 : JackRow));
                    jacks.Add(new JackLayout(new JackRef(module.Id, name), jack, at, CardJackRadius)
                    {
                        Label = StripLabel(strip, name), LabelAt = at + new Vector2(0, CardJackRadius + 13),
                    });
                }

                outX += CardSlot;
            }

            top += height + spare / 2;
        }

        var ofKind = patch.Modules.Where(m => m.Kind == module.Kind).ToList();
        var title = ofKind.Count > 1 ? $"{definition.Heading} {ofKind.IndexOf(module) + 1}" : definition.Heading;
        return new PanelLayout(module, title, bounds, knobs, jacks, display)
        {
            Buttons = buttons, Group = face.Group, Captions = captions, Meter = meter, StepLights = stepLights, Readout = readout,
        };
    }

    // A jack's name on its strip, without the strip's title it starts with
    private static string StripLabel(FaceStrip strip, string name) =>
        (strip.Title is { } title && name.StartsWith(title + " ", StringComparison.Ordinal) ? name[(title.Length + 1)..] : name).ToUpperInvariant();

    // The LIGHTS box: for each light a caption with its lamp, then its intensity, hue and saturation knobs, each over its CV jack
    private static PanelLayout LayOutLights(Module module, Box bounds)
    {
        var definition = module.Definition;
        var knobs = new List<KnobLayout>();
        var jacks = new List<JackLayout>();
        foreach (var (side, row) in new[] { (LightSide.Left, LightRows[0]), (LightSide.Right, LightRows[1]) })
        {
            var parameters = definition.Parameters.Where(p => p.Name.StartsWith(side.ToString(), StringComparison.Ordinal)).ToList();
            for (var i = 0; i < parameters.Count; i++)
            {
                var x = bounds.Min.X + Spread(i, parameters.Count, bounds.Size.X);
                var knob = new KnobLayout(module.Id, parameters[i], new Vector2(x, bounds.Min.Y + row + LightKnobDrop), KnobRadius);
                knobs.Add(knob);
                jacks.Add(new JackLayout(new JackRef(module.Id, parameters[i].CvJack), definition.FindJack(parameters[i].CvJack)!,
                    new Vector2(x, bounds.Min.Y + row + LightJackDrop), KnobJackRadius));
            }
        }

        return new PanelLayout(module, definition.Title, bounds, knobs, jacks, Place: PanelPlace.Desk);
    }

    /// <summary>The design y of a light's caption line in the LIGHTS box, from its top.</summary>
    public static float LightCaptionY(LightSide side) => side == LightSide.Left ? LightRows[0] : LightRows[1];

    // The cheek: the two wheels at its left, the octave's readout and buttons, and the three outputs along its front
    private static CheekLayout LayOutCheek(Module keyboard, Box bounds)
    {
        var definition = keyboard.Definition;
        var origin = bounds.Min;
        var wheelSize = new Vector2(38, 124);
        WheelLayout[] wheels =
        [
            new(keyboard.Id, definition.FindParameter(Modules.Bend)!, new Box(origin + new Vector2(22, 26), wheelSize), Springs: true),
            new(keyboard.Id, definition.FindParameter(Modules.Mod)!, new Box(origin + new Vector2(78, 26), wheelSize), Springs: false),
        ];
        // In a column from the back, so nothing tall hides a readout: the VOICES selector, its readout, then the octave's
        // readout over its buttons
        KnobLayout[] knobs = [new(keyboard.Id, definition.FindParameter(Modules.Voices)!, origin + new Vector2(180, 34), KnobRadius)];
        var voicesReadout = new Box(origin + new Vector2(132, 80), new Vector2(96, 26));
        var readout = new Box(origin + new Vector2(132, 114), new Vector2(96, 26));
        ButtonLayout[] buttons =
        [
            new(keyboard.Id, ButtonAction.OctaveDown, new Box(origin + new Vector2(136, 150), ButtonSize)),
            new(keyboard.Id, ButtonAction.OctaveUp, new Box(origin + new Vector2(190, 150), ButtonSize)),
        ];
        // The outputs in a column down the cheek's right, each labelled beside it
        var outputs = definition.Jacks.Where(j => j.Direction == JackDirection.Output).ToList();
        var jacks = outputs.Select((jack, i) => new JackLayout(new JackRef(keyboard.Id, jack.Name), jack, origin + new Vector2(252, 36 + i * 56), JackRadius)).ToList();
        return new CheekLayout(keyboard, bounds, wheels, buttons, readout, knobs, voicesReadout, jacks);
    }

    // Centres count items a slot apart, the group centred across the panel
    private static float Spread(int index, int count, float width) => (width - count * Slot) / 2 + (index + 0.5f) * Slot;

    private static List<PianoKeyLayout> LayOutPiano(Vector2 topLeft, float whiteWidth, int count)
    {
        var semitones = Enumerable.Range(0, count).ToList();
        var whites = semitones.Where(s => !KeyMap.IsBlack(s)).ToList();
        var keys = whites
            .Select((s, i) => new PianoKeyLayout(s, new Box(new Vector2(topLeft.X + i * whiteWidth, topLeft.Y), new Vector2(whiteWidth, WhiteKeyHeight)), false))
            .ToList();
        var blackWidth = whiteWidth * BlackKeyShare;
        foreach (var semitone in semitones.Where(KeyMap.IsBlack))
        {
            // A black key straddles the line between the white keys below and above it
            var whitesBelow = whites.Count(w => w < semitone);
            var x = topLeft.X + whitesBelow * whiteWidth - blackWidth / 2;
            keys.Add(new PianoKeyLayout(semitone, new Box(new Vector2(x, topLeft.Y), new Vector2(blackWidth, BlackKeyHeight)), true));
        }

        return keys;
    }
}

/// <summary>How the rack is seen: converts between its design units and pixels on the screen.</summary>
public interface IRackView
{
    Vector2 ToScreen(Vector2 design);

    Vector2 ToDesign(Vector2 screen);
}

/// <summary>Gives the view of a layout on a screen of a size, each frame.</summary>
public interface IRackViews
{
    IRackView Fit(RackLayout layout, Vector2 screen);
}

/// <summary>The layout laid flat on the screen, centred and as large as fits.</summary>
public readonly record struct RackView(float Scale, Vector2 Offset) : IRackView
{
    public static RackView Fit(Vector2 design, Vector2 screen)
    {
        var scale = MathF.Max(0.01f, MathF.Min(screen.X / design.X, screen.Y / design.Y));
        return new RackView(scale, (screen - design * scale) / 2);
    }

    public Vector2 ToScreen(Vector2 design) => Offset + design * Scale;

    public Vector2 ToDesign(Vector2 screen) => (screen - Offset) / Scale;
}
