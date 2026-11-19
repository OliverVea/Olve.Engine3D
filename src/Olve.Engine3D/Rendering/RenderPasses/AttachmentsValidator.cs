using Olve.Engine3D.Rendering.Textures;
using Olve.Utilities.IntegerMath2D;
using Olve.Validation.Validators.Base;

namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>Validates the textures a pass draws into.</summary>
internal sealed class AttachmentsValidator(TextureManager textureManager)
    : BaseValidator<IReadOnlyList<TextureId>, AttachmentsValidator>
{
    protected override AttachmentsValidator Validator => this;

    public AttachmentsValidator MustBeCreatedTextures() => FailIf(
        textures => GetUncreated(textures).Any(),
        textures => new ResultProblem("Attachments {0} are not created textures", string.Join(", ", GetUncreated(textures))));

    public AttachmentsValidator MustBeSameSize() => FailIf(
        textures => GetSizes(textures).Distinct().Count() > 1,
        textures => new ResultProblem("A pass's attachments must be the same size, but they are {0}", string.Join(", ", GetSizes(textures))));

    public AttachmentsValidator MustAgreeOnSamples() => FailIf(
        textures => textures.Where(texture => texture != TextureManager.Screen).Select(textureManager.IsMultisampled).Distinct().Count() > 1,
        _ => new ResultProblem("A pass's colour and depth attachments must both be multisampled or neither"));

    private IEnumerable<TextureId> GetUncreated(IReadOnlyList<TextureId> textures) =>
        textures.Where(texture => !textureManager.TryGetSize(texture, out _));

    private IEnumerable<Size> GetSizes(IReadOnlyList<TextureId> textures)
    {
        foreach (var texture in textures)
        {
            if (textureManager.TryGetSize(texture, out var size))
            {
                yield return size;
            }
        }
    }
}
