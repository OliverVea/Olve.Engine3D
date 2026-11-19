namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>
/// A texture format that can be sampled with a <typeparamref name="TKind"/> sampler.
/// </summary>
public interface ISampledWith<TKind> where TKind : ISamplerKind;
