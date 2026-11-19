using System.Text;
using System.Text.RegularExpressions;
using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Shaders as one WGSL module per stage, with where their inputs are bound.</summary>
internal sealed partial class WebGPUShaderBackend(Provider<WebGPUContext> contextProvider, WebGPUResources resources) : IShaderBackend
{
    public Result CreateShader(ShaderId shaderId, ShaderData shaderData)
    {
        if (resources.Shaders.ContainsKey(shaderId))
        {
            return new ResultProblem("Shader '{0}' already exists", shaderData.Name);
        }

        var wgsl = shaderData.Wgsl;
        if (GetEntryPoint(wgsl.VertexSource, VertexEntryPoint()).TryPickProblems(out var problems, out var vertexEntryPoint)
            || GetEntryPoint(wgsl.FragmentSource, FragmentEntryPoint()).TryPickProblems(out problems, out var fragmentEntryPoint))
        {
            return problems.Prepend("Shader '{0}' has no single entry point per stage", shaderData.Name);
        }

        var context = contextProvider.Value;
        if (CreateModule(context, wgsl.VertexSource).TryPickProblems(out problems, out var vertexModule))
        {
            return problems.Prepend("Vertex shader of '{0}' failed to compile", shaderData.Name);
        }

        if (CreateModule(context, wgsl.FragmentSource).TryPickProblems(out problems, out var fragmentModule))
        {
            vertexModule.Dispose();
            return problems.Prepend("Fragment shader of '{0}' failed to compile", shaderData.Name);
        }

        resources.Shaders[shaderId] = new WgpuShader(
            vertexModule, Encoding.UTF8.GetBytes(vertexEntryPoint), fragmentModule, Encoding.UTF8.GetBytes(fragmentEntryPoint),
            wgsl.Pass, wgsl.Group, GetDeclaredBindings(wgsl.VertexSource, wgsl.FragmentSource));
        return Result.Success();
    }

    public DeletionResult DeleteShader(ShaderId shaderId)
    {
        if (!resources.Shaders.Remove(shaderId, out var shader))
        {
            return DeletionResult.NotFound();
        }

        shader.Release();
        return DeletionResult.Success();
    }

    // The shader data doesn't name its entry points, so they are read from the source
    private static Result<string> GetEntryPoint(string source, Regex entryPoint) =>
        entryPoint.Matches(source) is [var match]
            ? match.Groups[1].Value
            : new ResultProblem("Expected one entry point matching '{0}'", entryPoint);

    // The inputs bindings list every binding of the shader's inputs; a stage's source declares only those it reads,
    // which are all the layout WebGPU derives from the shader holds
    private static IReadOnlySet<int> GetDeclaredBindings(string vertexSource, string fragmentSource) =>
        Binding().Matches(vertexSource).Concat(Binding().Matches(fragmentSource))
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

    private static Result<ShaderModule> CreateModule(WebGPUContext context, string source)
    {
        var descriptor = new ShaderModuleDescriptor
        {
            ShaderSourceWGSL = new ShaderSourceWGSL { Code = Encoding.UTF8.GetBytes(source) },
        };

        context.PushErrorScope();
        var module = context.Device.CreateShaderModule(descriptor);
        if (context.PopErrorScope("creating a shader module").TryPickProblems(out var problems))
        {
            module.Dispose();
            return problems;
        }

        return module;
    }

    [GeneratedRegex(@"@vertex\s+fn\s+(\w+)")]
    private static partial Regex VertexEntryPoint();

    [GeneratedRegex(@"@fragment\s+fn\s+(\w+)")]
    private static partial Regex FragmentEntryPoint();

    [GeneratedRegex(@"@binding\((\d+)\)\s*@group\(0\)")]
    private static partial Regex Binding();
}
