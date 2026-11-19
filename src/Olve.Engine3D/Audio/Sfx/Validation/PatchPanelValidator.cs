using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;
using Olve.Validation.Validators.Base;

namespace Olve.Engine3D.Audio.Sfx.Validation;

internal sealed class PatchPanelValidator : BaseValidator<PatchPanel, PatchPanelValidator>
{
    public static PatchPanelValidator Default { get; } = new PatchPanelValidator().MustHaveUniqueInputIds().MustCompile();

    protected override PatchPanelValidator Validator => this;

    public PatchPanelValidator MustHaveUniqueInputIds() => FailIf(
        panel => RepeatedInputs(panel).Any(),
        panel => new ResultProblem("Panel '{0}' has inputs that share ids: {1}", panel.Name, string.Join(", ", RepeatedInputs(panel))));

    public PatchPanelValidator MustCompile() => FailIf(
        panel => Compile(panel).TryPickProblems(out _),
        panel => new ResultProblem("Panel '{0}' is broken: {1}", panel.Name,
            Compile(panel).TryPickProblems(out var problems) ? string.Join("; ", problems.Select(problem => problem.FormattedMessage)) : ""));

    private static Result<PatchVoice> Compile(PatchPanel panel) =>
        PatchVoice.Compile(panel.Patch, SoundManager.SampleRate, new Dictionary<string, float>());

    private static IEnumerable<Id<PatchInput>> RepeatedInputs(PatchPanel panel)
    {
        var seen = new HashSet<Id<PatchInput>>();
        return panel.Patch.Parameters.Select(input => input.Id).Where(id => !seen.Add(id)).Distinct().ToList();
    }
}
