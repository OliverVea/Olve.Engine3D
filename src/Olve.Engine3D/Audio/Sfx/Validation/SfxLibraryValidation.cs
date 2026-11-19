using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Audio.Sfx.Validation;

public static class SfxLibraryValidation
{
    public static Result ValidateIds(SfxLibraryContent content)
    {
        var problems = IdProblems(content).ToList();
        return problems.Count == 0 ? Result.Success() : Result.Failure(problems);
    }

    public static Result Validate(SfxLibraryContent content)
    {
        var problems = IdProblems(content).ToList();
        var panels = new Dictionary<Id<PatchPanel>, PatchPanel>();
        foreach (var panel in content.Panels.Where(panel => panels.TryAdd(panel.Id, panel)))
        {
            if (PatchPanelValidator.Default.Validate(panel).TryPickProblems(out var panelProblems))
            {
                problems.AddRange(panelProblems);
            }
        }

        var sounds = new HashSet<Id<LibrarySound>>();
        var soundValidator = LibrarySoundValidator.For(panels.GetValueOrDefault);
        foreach (var sound in content.Sounds.Where(sound => sounds.Add(sound.Id)))
        {
            if (soundValidator.Validate(sound).TryPickProblems(out var soundProblems))
            {
                problems.AddRange(soundProblems);
            }
        }

        return problems.Count == 0 ? Result.Success() : Result.Failure(problems);
    }

    private static IEnumerable<ResultProblem> IdProblems(SfxLibraryContent content) =>
    [
        .. Repeated(content.Panels.Select(panel => panel.Id), id => new ResultProblem("Two panels have the id {0}", id)),
        .. Repeated(content.Sounds.Select(sound => sound.Id), id => new ResultProblem("Two sounds have the id {0}", id)),
        .. Repeated(content.Bindings.Select(binding => binding.Slot), id => new ResultProblem("Slot {0} is bound more than once", id)),
    ];

    private static IEnumerable<ResultProblem> Repeated<T>(IEnumerable<T> ids, Func<T, ResultProblem> problem)
    {
        var seen = new HashSet<T>();
        return ids.Where(id => !seen.Add(id)).Select(problem).ToList();
    }
}
