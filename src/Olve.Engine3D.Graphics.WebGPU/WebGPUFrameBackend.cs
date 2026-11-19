using System.Diagnostics;
using Olve.Engine3D.Diagnostics.Benchmarks;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Draws;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Submits a frame as one command buffer, skipping and reporting what can't be drawn; the host presents it.</summary>
internal sealed class WebGPUFrameBackend(
    Provider<WebGPUContext> contextProvider,
    WebGPUScreen screen,
    WebGPUResources resources,
    WebGPUGpuTimer gpuTimer,
    BenchmarkRecorder benchmarkRecorder,
    IWindowInfo windowInfo) : IFrameBackend
{
    // A pass's draws that can be drawn, gathered afresh for each pass
    private readonly List<WgpuDraw> _draws = [];

    public Result RenderFrame(Frame frame)
    {
        // Dawn runs callbacks (errors, device loss, mapping) and its deferred work inside ProcessEvents
        contextProvider.Value.Instance.ProcessEvents();
        if (contextProvider.Value.CheckDevice().TryPickProblems(out var problems))
        {
            return problems;
        }

        gpuTimer.Collect();

        // Acquiring waits for the GPU to free one of the surface's textures, so it's waiting, not CPU work
        var acquireStart = Stopwatch.GetTimestamp();
        var acquired = screen.Acquire(windowInfo.ContentAreaSize);
        benchmarkRecorder.RecordWait(Stopwatch.GetElapsedTime(acquireStart));
        if (acquired.TryPickProblems(out problems))
        {
            return problems;
        }

        var context = contextProvider.Value;
        using var encoder = context.Device.CreateCommandEncoder();
        List<ResultProblem> frameProblems = [];

        // With multisampling, only the frame's last pass into the screen, or into a multisampled texture, resolves into it
        var lastPasses = LastPassesIntoResolved(frame);
        var timing = gpuTimer.Begin();
        var passes = frame.Passes;
        for (var i = 0; i < passes.Count; i++)
        {
            var framePass = passes[i];
            var resolves = lastPasses.Contains(framePass.Pass);
            var timestampWrites = timing?.GetWrites(isFirst: i == 0, isLast: i == passes.Count - 1);
            if (RenderPass(context, encoder, framePass, resolves, timestampWrites, frameProblems).TryPickProblems(out var passProblems))
            {
                frameProblems.AddRange(passProblems.Prepend("Skipped pass '{0}'", framePass.Pass));
            }
        }

        timing?.Resolve(encoder);

        using var commands = encoder.Finish();
        context.Queue.Submit([commands]);
        timing?.Read();

        return frameProblems.Count == 0 ? Result.Success() : new ResultProblemCollection(frameProblems);
    }

    // For the screen and each multisampled texture shaders read, the frame's last pass drawing into it
    private HashSet<PassId> LastPassesIntoResolved(Frame frame)
    {
        Dictionary<TextureId, PassId> last = [];
        foreach (var framePass in frame.Passes)
        {
            if (resources.Passes.TryGetValue(framePass.Pass, out var pass) && pass.Color is { } color
                && (color.Texture == TextureManager.Screen || resources.Textures.TryGetValue(color.Texture, out var texture) && texture.Samples is not null))
            {
                last[color.Texture] = framePass.Pass;
            }
        }

        return [.. last.Values];
    }

    private Result RenderPass(
        WebGPUContext context,
        CommandEncoder encoder,
        FramePass framePass,
        bool resolves,
        PassTimestampWrites? timestampWrites,
        List<ResultProblem> drawProblems)
    {
        if (!resources.Passes.TryGetValue(framePass.Pass, out var pass))
        {
            return new ResultProblem("Pass '{0}' has no WebGPU state", framePass.Pass);
        }

        // Buffer writes are queued before the frame's commands, and bind groups are built on the device, so both happen
        // before the pass is encoded; a draw whose inputs can't be bound is skipped
        pass.Inputs.UploadIfDirty(context);
        var draws = _draws;
        draws.Clear();
        foreach (var drawId in framePass.Draws)
        {
            if (Prepare(context, drawId, pass).TryPickProblems(out var problems, out var draw))
            {
                drawProblems.AddRange(problems.Prepend("Skipped draw '{0}'", drawId));
                continue;
            }

            draws.Add(draw);
        }

        if (GetColorAttachments(pass, resolves).TryPickProblems(out var attachmentProblems, out var colorAttachments)
            || GetDepthAttachment(pass).TryPickProblems(out attachmentProblems, out var depthAttachment))
        {
            return attachmentProblems;
        }

        using var passEncoder = encoder.BeginRenderPass(new RenderPassDescriptor
        {
            ColorAttachments = colorAttachments,
            DepthStencilAttachment = depthAttachment,
            TimestampWrites = timestampWrites,
        });

        foreach (var draw in draws)
        {
            Draw(passEncoder, draw);
        }

        passEncoder.End();
        return Result.Success();
    }

    private Result<RenderPassColorAttachment[]> GetColorAttachments(WgpuPass pass, bool resolves)
    {
        if (pass.Color is not { } color)
        {
            return Array.Empty<RenderPassColorAttachment>();
        }

        if (GetView(color.Texture).TryPickProblems(out var problems, out var view))
        {
            return problems.Prepend("Colour attachment '{0}' can't be drawn into", color.Texture);
        }

        var attachment = new RenderPassColorAttachment
        {
            View = view,
            LoadOp = color.Clear is null ? LoadOp.Load : LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = color.Clear ?? new Color(),
        };

        // Multisampled passes draw into the samples, which the last one resolves and then discards: the screen's, or a
        // multisampled texture's
        var samples = color.Texture == TextureManager.Screen
            ? screen.SampleCount > 1 ? screen.GetMultisampledView() : default
            : resources.Textures.TryGetValue(color.Texture, out var texture) && texture.Samples is { } textureSamples ? textureSamples.View : default;
        if (samples != default)
        {
            attachment = attachment with
            {
                View = samples,
                ResolveTarget = resolves ? view : default,
                StoreOp = resolves ? StoreOp.Discard : StoreOp.Store,
            };
        }

        return new[] { attachment };
    }

    private Result<RenderPassDepthStencilAttachment?> GetDepthAttachment(WgpuPass pass)
    {
        if (pass.Depth is not { } depth)
        {
            return Result<RenderPassDepthStencilAttachment?>.Success(null);
        }

        TextureView view;
        if (depth.Texture is null)
        {
            view = screen.GetDepthView();
        }
        else if (GetView(depth.Texture).TryPickProblems(out var problems, out view))
        {
            return problems.Prepend("Depth attachment '{0}' can't be drawn into", depth.Texture);
        }

        return new RenderPassDepthStencilAttachment
        {
            View = view,
            DepthLoadOp = depth.Clear is null ? LoadOp.Load : LoadOp.Clear,
            DepthStoreOp = StoreOp.Store,
            DepthClearValue = depth.Clear ?? Wgpu.DepthClearValueUndefined,
        };
    }

    private Result<TextureView> GetView(TextureId textureId)
    {
        if (textureId == TextureManager.Screen)
        {
            return screen.View;
        }

        return resources.Textures.TryGetValue(textureId, out var texture)
            ? texture.View
            : new ResultProblem("Texture '{0}' has no WebGPU texture", textureId);
    }

    private Result<WgpuDraw> Prepare(WebGPUContext context, DrawId drawId, WgpuPass pass)
    {
        if (!resources.Draws.TryGetValue(drawId, out var draw))
        {
            return new ResultProblem("Draw '{0}' has no WebGPU pipeline", drawId);
        }

        if (!resources.Groups.TryGetValue(draw.Group, out var group))
        {
            return new ResultProblem("Group '{0}' has no WebGPU state", draw.Group);
        }

        if (!resources.Meshes.ContainsKey(group.Mesh))
        {
            return new ResultProblem("Mesh '{0}' has no WebGPU buffers", group.Mesh);
        }

        group.UploadIfDirty(context);
        draw.Inputs.UploadIfDirty(context);
        if (WebGPUBindGroups.Update(context, resources, draw, pass).TryPickProblems(out var problems))
        {
            return problems;
        }

        return draw;
    }

    private void Draw(RenderPassEncoder passEncoder, WgpuDraw draw)
    {
        var group = resources.Groups[draw.Group];
        var mesh = resources.Meshes[group.Mesh];
        if (group.InstanceCount == 0 || mesh.VertexCount == 0)
        {
            return;
        }

        passEncoder.SetPipeline(draw.Pipeline);
        if (draw.BindGroup != default)
        {
            passEncoder.SetBindGroup(0, draw.BindGroup);
        }

        // The same slots as the pipeline: the mesh's vertices, then the group's instances, skipping those without floats
        var slot = 0u;
        if (mesh.VertexBuffer != default)
        {
            passEncoder.SetVertexBuffer(slot++, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        }

        if (group.InstanceBuffer != default)
        {
            passEncoder.SetVertexBuffer(slot, group.InstanceBuffer, 0, Wgpu.WholeSize);
        }

        var instanceCount = (uint)group.InstanceCount;
        if (mesh.IndexBuffer != default)
        {
            passEncoder.SetIndexBuffer(mesh.IndexBuffer, IndexFormat.Uint32, 0, mesh.IndexBufferSize);
            passEncoder.DrawIndexed(mesh.IndexCount, instanceCount);
        }
        else
        {
            passEncoder.Draw(mesh.VertexCount, instanceCount);
        }
    }
}
