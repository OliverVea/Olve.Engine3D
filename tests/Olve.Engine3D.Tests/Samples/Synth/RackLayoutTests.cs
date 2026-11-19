using System.Numerics;
using Olve.Engine3D.Samples.Synth.Patching;
using Olve.Engine3D.Samples.Synth.Rack;

namespace Olve.Engine3D.Tests.Samples.Synth;

public class RackLayoutTests
{
    private static bool Inside(Box box, Vector2 centre, float radius) =>
        centre.X - radius >= box.Min.X && centre.Y - radius >= box.Min.Y && centre.X + radius <= box.Max.X && centre.Y + radius <= box.Max.Y;

    private static bool Overlap(Box a, Box b) => a.Min.X < b.Max.X && b.Min.X < a.Max.X && a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;

    private static Box Around(Vector2 centre, float radius) => new(centre - new Vector2(radius), new Vector2(2 * radius));

    [Test]
    [Arguments(RackLayout.WideOctaves)]
    [Arguments(RackLayout.NarrowOctaves)]
    public async Task Every_control_sits_in_its_own_panel_or_cheek_and_no_two_are_touched_at_once(int octaves)
    {
        var layout = new RackLayout(Presets.Classic(), octaves);
        var panels = layout.Panels;
        foreach (var panel in panels)
        {
            await Assert.That(panel.Knobs.All(k => Inside(panel.Bounds, k.Centre, k.Radius))).IsTrue();
            await Assert.That(panel.Jacks.All(j => Inside(panel.Bounds, j.Centre, j.Radius))).IsTrue();
            // A knob's own jack, if it has one, is right under it
            foreach (var knob in panel.Knobs.Where(k => panel.Module.Definition.FindJack(k.Parameter.CvJack) is not null))
            {
                var jack = panel.Jacks.Single(j => j.Definition.Knob == knob.Parameter.Name);
                await Assert.That(jack.Centre.X).IsEqualTo(knob.Centre.X);
                await Assert.That(jack.Centre.Y).IsGreaterThan(knob.Centre.Y);
            }
        }

        var cheek = layout.Cheek!;
        await Assert.That(cheek.Jacks.All(j => Inside(cheek.Bounds, j.Centre, j.Radius))).IsTrue();
        await Assert.That(cheek.Wheels.Select(w => w.Bounds).Concat(cheek.Buttons.Select(b => b.Bounds)).Append(cheek.Readout)
            .All(b => Inside(cheek.Bounds, b.Centre, 0) && Vector2.Min(b.Min, cheek.Bounds.Min) == cheek.Bounds.Min && Vector2.Max(b.Max, cheek.Bounds.Max) == cheek.Bounds.Max)).IsTrue();

        // The side case's cards are in its face
        await Assert.That(layout.SidePanels.Select(p => p.Module.Kind)).IsEquivalentTo([ModuleKind.Sequencer, ModuleKind.Quantizer], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(layout.SidePanels.All(p => Inside(layout.Side!.Value, p.Bounds.Centre, 0) && Vector2.Max(p.Bounds.Max, layout.Side.Value.Max) == layout.Side.Value.Max)).IsTrue();

        // Panels, the speakers, the cheek, the keys, the LIGHTS box and the side case never overlap
        var areas = panels.Where(p => p.Place != PanelPlace.Side).Select(p => p.Bounds).Concat(layout.Speakers).Append(cheek.Bounds).Append(layout.Keys).Append(layout.Side!.Value).ToList();
        await Assert.That(areas.SelectMany((a, i) => areas.Skip(i + 1).Where(b => Overlap(a, b)))).IsEmpty();

        // Hit areas, with the slack the hit test gives them, never overlap
        var targets = layout.Jacks.Select(j => Around(j.Centre, j.Radius + j.Slack))
            .Concat(layout.Knobs.Select(k => k.Reach))
            .Concat(panels.SelectMany(p => p.StepLights).Select(l => Around(l, 3)))
            .Concat(panels.Where(p => p.Readout is not null).Select(p => p.Readout!.Value))
            .Concat(layout.Buttons.Select(b => b.Bounds))
            .Concat(layout.Wheels.Select(w => w.Bounds))
            .Concat(panels.Where(p => p.Display is not null).Select(p => p.Display!.Value))
            .ToList();
        await Assert.That(targets.SelectMany((a, i) => targets.Skip(i + 1).Where(b => Overlap(a, b)))).IsEmpty();

        await Assert.That(layout.PianoKeys.All(k => k.Bounds.Min.Y >= layout.RackPanels.Max(p => p.Bounds.Max.Y))).IsTrue();
        await Assert.That(layout.Desk!.Bounds.Min.X).IsGreaterThan(layout.CaseWidth);

        // A speaker at each end of the rows, as tall as both
        var rack = layout.RackPanels.ToList();
        await Assert.That(layout.Speakers[0].Max.X).IsLessThan(rack.Min(p => p.Bounds.Min.X));
        await Assert.That(layout.Speakers[1].Min.X).IsGreaterThan(rack.Max(p => p.Bounds.Max.X));
        await Assert.That(layout.Speakers.All(s => s.Min.Y == rack.Min(p => p.Bounds.Min.Y) && s.Max.Y == rack.Max(p => p.Bounds.Max.Y))).IsTrue();
    }

    // Every module with a card of its own; the keyboard's controls are on the cheek and the scopes have none
    public static IEnumerable<ModuleKind> Carded() => Enum.GetValues<ModuleKind>().Where(k => k is not (ModuleKind.Keyboard or ModuleKind.Scope));

    [Test]
    [MethodDataSource(nameof(Carded))]
    public async Task A_card_shows_each_of_its_modules_jacks_and_knobs_exactly_once(ModuleKind kind)
    {
        var patch = new SynthPatch();
        var module = patch.Add(kind);
        var panel = new RackLayout(patch).Panels.Single(p => p.Module.Id == module.Id);

        await Assert.That(panel.Jacks.Where(j => !j.Definition.IsKnobInput).Select(j => j.Jack.Jack))
            .IsEquivalentTo(module.Definition.Jacks.Where(j => !j.IsKnobInput).Select(j => j.Name));
        await Assert.That(panel.Knobs.Select(k => k.Parameter.Name)).IsEquivalentTo(module.Definition.Parameters.Select(p => p.Name));
    }

    [Test]
    public async Task A_rail_runs_under_each_card_with_another_right_under_it_and_nowhere_else()
    {
        var layout = new RackLayout(Presets.Classic());
        var cards = layout.RackPanels.Select(p => p.Bounds).ToList();
        var stacked = cards.Count(upper => cards.Any(below => below.Min.X == upper.Min.X && below.Min.Y > upper.Max.Y
            && !cards.Any(between => between.Min.X == upper.Min.X && between.Min.Y > upper.Max.Y && between.Min.Y < below.Min.Y)));

        await Assert.That(layout.Rails.Count()).IsEqualTo(stacked);
        foreach (var rail in layout.Rails)
        {
            await Assert.That(cards.Count(card => Overlap(rail, card))).IsEqualTo(0).Because("a rail sits in the gap between two cards");
            await Assert.That(cards.Count(card => card.Max.Y == rail.Min.Y && card.Min.X > rail.Min.X && card.Max.X < rail.Max.X)).IsEqualTo(1);
            await Assert.That(cards.Count(card => card.Min.Y == rail.Max.Y && card.Min.X > rail.Min.X && card.Max.X < rail.Max.X)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task The_cards_stand_in_columns_along_the_path_of_the_sound_with_the_lights_on_the_desk()
    {
        var layout = new RackLayout(Presets.Classic());
        var columns = layout.RackPanels.GroupBy(p => p.Bounds.Min.X).OrderBy(c => c.Key)
            .Select(c => c.OrderBy(p => p.Bounds.Min.Y).Select(p => p.Title).ToList()).ToList();

        string[][] expected =
        [
            ["OSCILLATORS", "LFO 1", "LFO 2", "RANDOM", "LINE IN · FOLLOW"],
            ["FILTER", "VCA · RING", "ENV 1", "ENV 2"],
            ["DRIVE/FOLD", "PHASER", "DELAY/CHO", "REVERB"],
            ["MIXER", "OUT", "SLEW · GLIDE", "ATTEN 1", "ATTEN 2"],
        ];
        await Assert.That(columns.Select(c => string.Join(", ", c))).IsEquivalentTo(expected.Select(c => string.Join(", ", c)), TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(layout.Desk!.Title).IsEqualTo("LIGHTS");
        await Assert.That(layout.Cheek!.Jacks.Select(j => j.Jack.Jack)).IsEquivalentTo([Modules.Pitch, Modules.Gate, Modules.Mod], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task A_card_has_its_inputs_left_and_its_outputs_right_and_an_effect_takes_its_input_above_its_output()
    {
        var layout = new RackLayout(Presets.Classic());
        foreach (var panel in layout.RackPanels.Where(p => p.Knobs.Count > 0))
        {
            var inputs = panel.Jacks.Where(j => j.Definition is { Direction: JackDirection.Input, IsKnobInput: false }).ToList();
            var outputs = panel.Jacks.Where(j => j.Definition.Direction == JackDirection.Output).ToList();
            var controls = panel.Knobs.Select(k => k.Centre.X).ToList();
            await Assert.That(inputs.All(j => j.Centre.X < controls.Min())).IsTrue();
            await Assert.That(outputs.All(j => j.Centre.X > controls.Max())).IsTrue();
            if (panel.Group == ModuleGroup.Effects)
            {
                await Assert.That(inputs.Max(j => j.Centre.Y)).IsLessThan(outputs.Min(j => j.Centre.Y));
            }
        }

        // The envelopes and the mixer have faders; the filter's cutoff and the output's volume are big knobs
        var rack = layout.RackPanels.ToList();
        await Assert.That(rack.Where(p => p.Module.Kind is ModuleKind.Envelope or ModuleKind.Mixer).SelectMany(p => p.Knobs).All(k => k.IsFader)).IsTrue();
        await Assert.That(rack.SelectMany(p => p.Knobs).Where(k => k.Radius == RackLayout.BigKnobRadius).Select(k => k.Parameter.Name))
            .IsEquivalentTo([Modules.Cutoff, Modules.Volume], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task The_side_case_leaves_the_rack_as_it_was()
    {
        var without = new SynthPatch();
        foreach (var module in Presets.Classic().Modules.Where(m => !RackLayout.InSideCase(m.Kind)))
        {
            without.Add(module.Kind);
        }

        var rack = new RackLayout(without);
        var withSide = new RackLayout(Presets.Classic());

        await Assert.That(rack.Side).IsNull();
        await Assert.That(withSide.CaseWidth).IsEqualTo(rack.CaseWidth);
        await Assert.That(withSide.RackPanels.Select(p => p.Bounds)).IsEquivalentTo(rack.RackPanels.Select(p => p.Bounds), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task The_sequencer_lines_each_steps_gate_button_and_light_up_under_its_pitch_knob_with_the_transport_under_them()
    {
        var sequencer = new RackLayout(Presets.Classic()).SidePanels.Single(p => p.Module.Kind == ModuleKind.Sequencer);
        var knobs = sequencer.Knobs.ToDictionary(k => k.Parameter.Name);

        await Assert.That(sequencer.StepLights.Count).IsEqualTo(Modules.SequencerSteps);
        for (var step = 1; step <= Modules.SequencerSteps; step++)
        {
            var (pitch, gate, light) = (knobs[Modules.StepPitch(step)], knobs[Modules.StepGate(step)], sequencer.StepLights[step - 1]);
            await Assert.That(gate.IsButton && !pitch.IsButton).IsTrue();
            await Assert.That((gate.Centre.X, light.X)).IsEqualTo((pitch.Centre.X, pitch.Centre.X));
            await Assert.That(pitch.Centre.Y < light.Y && light.Y < gate.Centre.Y).IsTrue();
        }

        // The steps run left to right; RUN is a button, and the transport sits under the steps with its CV inputs
        await Assert.That(sequencer.StepLights.Select(l => l.X)).IsOrderedBy(x => x);
        await Assert.That(knobs[Modules.Run].IsButton).IsTrue();
        var stepsBottom = knobs[Modules.StepGate(1)].Centre.Y;
        await Assert.That(new[] { Modules.Run, Modules.Tempo, Modules.Length }.All(k => knobs[k].Centre.Y > stepsBottom)).IsTrue();
        await Assert.That(sequencer.Jacks.Count(j => j.Definition.IsKnobInput)).IsEqualTo(3);
    }
}
