using Olve.Validation.Validators.Base;

namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>Validates a sampler description against the sampler's kind <typeparamref name="TKind"/>.</summary>
internal sealed class SamplerDescriptionValidator<TKind> : BaseStructValidator<SamplerDescription, SamplerDescriptionValidator<TKind>>
    where TKind : ISamplerKind
{
    protected override SamplerDescriptionValidator<TKind> Validator => this;

    public SamplerDescriptionValidator<TKind> MustSuitKind() => FailIf(
        description => typeof(TKind) == typeof(ISamplerKind.NonFiltering) && description.Filter != TextureFilter.Nearest,
        description => new ResultProblem("A non-filtering sampler must use nearest filtering, not {0}", description.Filter));
}
