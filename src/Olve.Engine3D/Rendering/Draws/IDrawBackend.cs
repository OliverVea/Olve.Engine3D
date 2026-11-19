using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Shaders;

namespace Olve.Engine3D.Rendering.Draws;

/// <summary>Creates and deletes draws on the GPU (a pipeline and its inputs) and holds their inputs.</summary>
public interface IDrawBackend
{
    Result CreateDraw<TInputs>(
        DrawId<TInputs> drawId,
        PassId passId,
        ShaderId shaderId,
        GroupId groupId,
        RenderState renderState,
        TInputs inputs)
        where TInputs : IGroupInputs;

    Result SetInputs<TInputs>(DrawId<TInputs> drawId, TInputs inputs)
        where TInputs : IGroupInputs;

    DeletionResult DeleteDraw(DrawId drawId);
}
