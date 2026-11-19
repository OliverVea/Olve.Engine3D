namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>Creates and deletes render passes on the GPU and holds their inputs.</summary>
public interface IPassBackend
{
    Result CreatePass<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, PassAttachments attachments, TPassInputs inputs)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs;

    Result SetInputs<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, TPassInputs inputs)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs;

    DeletionResult DeletePass(PassId passId);
}
