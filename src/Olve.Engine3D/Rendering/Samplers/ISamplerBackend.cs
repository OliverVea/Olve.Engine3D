namespace Olve.Engine3D.Rendering.Samplers;

/// <summary>Creates and deletes samplers on the GPU.</summary>
public interface ISamplerBackend
{
    Result CreateSampler<TKind>(SamplerId<TKind> samplerId, SamplerDescription description)
        where TKind : ISamplerKind;

    DeletionResult DeleteSampler(SamplerId samplerId);
}
