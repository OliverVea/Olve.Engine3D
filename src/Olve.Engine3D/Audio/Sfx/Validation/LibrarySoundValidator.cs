using Olve.Engine3D.Audio.Sfx.Library;
using Olve.Engine3D.Audio.Synthesis;
using Olve.Utilities.Ids;
using Olve.Validation.Validators.Base;

namespace Olve.Engine3D.Audio.Sfx.Validation;

internal sealed class LibrarySoundValidator : BaseValidator<LibrarySound, LibrarySoundValidator>
{
    private readonly Func<Id<PatchPanel>, PatchPanel?> _panels;

    private LibrarySoundValidator(Func<Id<PatchPanel>, PatchPanel?> panels)
    {
        _panels = panels;
    }

    public static LibrarySoundValidator For(Func<Id<PatchPanel>, PatchPanel?> panels) =>
        new LibrarySoundValidator(panels).CannotLastANegativeTime().MustComeFromAPanelInTheLibrary().MustSetOnlyItsPanelsInputs();

    protected override LibrarySoundValidator Validator => this;

    public LibrarySoundValidator CannotLastANegativeTime() => FailIf(
        sound => sound.Length < TimeSpan.Zero,
        sound => new ResultProblem("Sound '{0}' can't last a negative time ({1})", sound.Name, sound.Length));

    public LibrarySoundValidator MustComeFromAPanelInTheLibrary() => FailIf(
        sound => _panels(sound.Panel) is null,
        sound => new ResultProblem("Sound '{0}' is made from panel {1}, which isn't in the library", sound.Name, sound.Panel));

    public LibrarySoundValidator MustSetOnlyItsPanelsInputs() => FailIf(
        sound => UnknownInputs(sound).Any(),
        sound => new ResultProblem("Sound '{0}' sets inputs that panel '{1}' doesn't have: {2}",
            sound.Name, _panels(sound.Panel)?.Name ?? "", string.Join(", ", UnknownInputs(sound))));

    private IEnumerable<Id<PatchInput>> UnknownInputs(LibrarySound sound)
    {
        if (_panels(sound.Panel) is not { } panel)
        {
            return [];
        }

        var inputs = panel.Patch.Parameters.Select(input => input.Id).ToHashSet();
        return sound.Inputs.Keys.Where(input => !inputs.Contains(input)).OrderBy(input => input.Value.Value).ToList();
    }
}
