using Microsoft.Extensions.Logging;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Passes as their attachments, clears and inputs; a pass into the screen tests depth in a buffer sized to the screen.</summary>
internal sealed class WebGPUPassBackend(
    Provider<WebGPUContext> contextProvider,
    WebGPUScreen screen,
    WebGPUResources resources,
    ILogger<WebGPUPassBackend> logger) : IPassBackend
{
    public Result CreatePass<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, PassAttachments attachments, TPassInputs inputs)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
    {
        if (resources.Passes.ContainsKey(passId))
        {
            return new ResultProblem("Pass '{0}' already exists", passId);
        }

        if (GetColor(attachments.Color).TryPickProblems(out var problems, out var color))
        {
            return problems;
        }

        var drawsIntoScreen = attachments.Color?.Texture == TextureManager.Screen;
        PassDepth? depth = null;
        if (attachments.Depth is { } depthAttachment)
        {
            if (drawsIntoScreen)
            {
                logger.LogWarning("Pass {PassId} draws into the screen, so it uses a depth buffer sized to the screen, not texture {TextureId}",
                    passId, depthAttachment.Texture);
            }
            else if (!resources.Textures.ContainsKey(depthAttachment.Texture))
            {
                return new ResultProblem("Depth attachment '{0}' has no WebGPU texture", depthAttachment.Texture);
            }

            var clear = depthAttachment.Load is Load<ITextureFormat.Depth>.Clear(var value) ? value.Value : (float?)null;
            depth = new PassDepth(drawsIntoScreen ? null : depthAttachment.Texture, clear);
        }

        resources.Passes[passId] = new WgpuPass(color, depth, WgpuInputs.Create(contextProvider.Value, inputs));
        return Result.Success();
    }

    public Result SetInputs<TColor, TPassInputs>(PassId<TColor, TPassInputs> passId, TPassInputs inputs)
        where TColor : IColorFormat
        where TPassInputs : IPassInputs
    {
        if (!resources.Passes.TryGetValue(passId, out var pass))
        {
            return new ResultProblem("Pass '{0}' has no WebGPU state", passId);
        }

        pass.Inputs.Set(inputs);
        return Result.Success();
    }

    public DeletionResult DeletePass(PassId passId)
    {
        if (!resources.Passes.Remove(passId, out var pass))
        {
            return DeletionResult.NotFound();
        }

        pass.Release();
        return DeletionResult.Success();
    }

    private Result<PassColor?> GetColor(ColorAttachment? attachment)
    {
        if (attachment is null)
        {
            return Result<PassColor?>.Success(null);
        }

        TextureFormat format;
        if (attachment.Texture == TextureManager.Screen)
        {
            format = screen.Format;
        }
        else if (resources.Textures.TryGetValue(attachment.Texture, out var texture))
        {
            format = texture.Formats.Format;
        }
        else
        {
            return new ResultProblem("Colour attachment '{0}' has no WebGPU texture", attachment.Texture);
        }

        Color? clear = attachment switch
        {
            ColorAttachment<ITextureFormat.Rgba8Unorm> { Load: Load<ITextureFormat.Rgba8Unorm>.Clear(var value) } =>
                new Color { R = value.R / 255d, G = value.G / 255d, B = value.B / 255d, A = value.A / 255d },
            ColorAttachment<ITextureFormat.R32Float> { Load: Load<ITextureFormat.R32Float>.Clear(var value) } =>
                new Color { R = value.Value },
            _ => null,
        };

        return new PassColor(attachment.Texture, format, clear);
    }
}
