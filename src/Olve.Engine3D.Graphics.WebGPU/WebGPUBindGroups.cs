using System.Runtime.InteropServices;
using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.Textures;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>A draw's bind group 0: its pass's and its own inputs, at the bindings its shader's WGSL declares.</summary>
internal static class WebGPUBindGroups
{
    // Each draw's entries are worked out every frame, into these, and the bind group built again only when they change
    private static readonly List<BindGroupEntry> Entries = [];
    private static readonly List<BoundObject> BoundObjects = [];

    // Builds the bind group again only when the buffers, textures or samplers it binds have changed
    public static Result Update(WebGPUContext context, WebGPUResources resources, WgpuDraw draw, WgpuPass pass)
    {
        if (!resources.Shaders.TryGetValue(draw.Shader, out var shader))
        {
            return new ResultProblem("Shader '{0}' has no WebGPU modules", draw.Shader);
        }

        var entries = Entries;
        var boundObjects = BoundObjects;
        entries.Clear();
        boundObjects.Clear();
        if (AddEntries(resources, shader.PassBindings, shader.ReadBindings, pass.Inputs, entries, boundObjects).TryPickProblems(out var problems))
        {
            return problems.Prepend("Failed to bind the pass's inputs");
        }

        if (AddEntries(resources, shader.GroupBindings, shader.ReadBindings, draw.Inputs, entries, boundObjects).TryPickProblems(out problems))
        {
            return problems.Prepend("Failed to bind the draw's inputs");
        }

        if (entries.Count == 0 || CollectionsMarshal.AsSpan(boundObjects).SequenceEqual(draw.BoundObjects))
        {
            return Result.Success();
        }

        using var layout = draw.Pipeline.GetBindGroupLayout(0);
        context.PushErrorScope();
        var bindGroup = context.Device.CreateBindGroup(new BindGroupDescriptor { Layout = layout, Entries = entries.ToArray() });
        if (context.PopErrorScope("creating a draw's bind group").TryPickProblems(out problems))
        {
            bindGroup.Dispose();
            return problems;
        }

        draw.SetBindGroup(bindGroup, [.. boundObjects]);
        return Result.Success();
    }

    private static Result AddEntries(
        WebGPUResources resources,
        WgslInputsBindings bindings,
        IReadOnlySet<int> readBindings,
        WgpuInputs inputs,
        List<BindGroupEntry> entries,
        List<BoundObject> boundObjects)
    {
        if (bindings.Values is { } valuesBinding && readBindings.Contains(valuesBinding))
        {
            if (inputs.Buffer == default)
            {
                return new ResultProblem("The shader reads values at binding {0}, but the inputs have none", valuesBinding);
            }

            entries.Add(new BindGroupEntry { Binding = (uint)valuesBinding, Buffer = inputs.Buffer });
            boundObjects.Add(new BoundObject(valuesBinding, null, null, 0));
        }

        for (var i = 0; i < bindings.Textures.Count; i++)
        {
            if (bindings.Textures[i] is not { } textureBinding || !readBindings.Contains(textureBinding.Texture))
            {
                continue;
            }

            if (i >= inputs.Textures.Length)
            {
                return new ResultProblem("The shader reads texture {0}, but the inputs have {1}", i, inputs.Textures.Length);
            }

            var (textureId, samplerId) = inputs.Textures[i];
            if (textureId is null || !resources.Textures.TryGetValue(textureId, out var texture))
            {
                return new ResultProblem("Texture '{0}' has no WebGPU texture", textureId?.ToString() ?? "(none)");
            }

            entries.Add(new BindGroupEntry { Binding = (uint)textureBinding.Texture, TextureView = texture.View });
            boundObjects.Add(new BoundObject(textureBinding.Texture, textureId, null, texture.Generation));

            if (textureBinding.Sampler is not { } samplerBinding)
            {
                continue;
            }

            if (samplerId is null || !resources.Samplers.TryGetValue(samplerId, out var sampler))
            {
                return new ResultProblem("Sampler '{0}' has no WebGPU sampler", samplerId?.ToString() ?? "(none)");
            }

            entries.Add(new BindGroupEntry { Binding = (uint)samplerBinding, Sampler = sampler });
            boundObjects.Add(new BoundObject(samplerBinding, null, samplerId, 0));
        }

        return Result.Success();
    }
}

/// <summary>
/// What a bind group binds at one binding, by engine id: ids are never reused, while a freed handle can come back for a new object.
/// A values buffer belongs to its inputs, which the draw keeps, so its binding alone names it. A texture made again at a
/// new size keeps its id, so its generation tells the two apart.
/// </summary>
internal readonly record struct BoundObject(int Binding, TextureId? Texture, SamplerId? Sampler, int Generation);
