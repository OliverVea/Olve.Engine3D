using Olve.Validation.Validators.Base;

namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>Validates an inputs value before it reaches the backend; every declared texture is bound for each draw.</summary>
internal sealed class InputsValidator<TInputs> : BaseValidator<TInputs, InputsValidator<TInputs>>
    where TInputs : IInputs
{
    public static InputsValidator<TInputs> Default { get; } = new InputsValidator<TInputs>().MustSetAllTextures();

    protected override InputsValidator<TInputs> Validator => this;

    public InputsValidator<TInputs> MustSetAllTextures() => FailIf(
        inputs => GetUnsetTextures(inputs).Any(),
        inputs => new ResultProblem("The inputs leave textures {0} of {1} unset",
            string.Join(", ", GetUnsetTextures(inputs)), TInputs.TextureCount));

    private static IEnumerable<int> GetUnsetTextures(TInputs inputs)
    {
        var textures = new TextureBinding[TInputs.TextureCount];
        inputs.WriteTextures(textures);
        return Enumerable.Range(0, textures.Length).Where(i => textures[i].Texture is null);
    }
}
