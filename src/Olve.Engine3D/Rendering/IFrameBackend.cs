namespace Olve.Engine3D.Rendering;

/// <summary>Draws a frame on the GPU, exactly in the order given.</summary>
public interface IFrameBackend
{
    Result RenderFrame(Frame frame);
}
