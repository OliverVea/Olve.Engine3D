using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Samplers as WebGPU samplers; comparison samplers pass where the reference is less than or equal to the texel.</summary>
internal sealed class WebGPUSamplerBackend(Provider<WebGPUContext> contextProvider, WebGPUResources resources) : ISamplerBackend
{
    public Result CreateSampler<TKind>(SamplerId<TKind> samplerId, SamplerDescription description)
        where TKind : ISamplerKind
    {
        if (resources.Samplers.ContainsKey(samplerId))
        {
            return new ResultProblem("Sampler '{0}' already exists", samplerId);
        }

        var filter = description.Filter == TextureFilter.Linear ? FilterMode.Linear : FilterMode.Nearest;
        var wrap = description.Wrap == TextureWrap.Repeat ? AddressMode.Repeat : AddressMode.ClampToEdge;
        var descriptor = new SamplerDescriptor
        {
            AddressModeU = wrap,
            AddressModeV = wrap,
            MagFilter = filter,
            MinFilter = filter,
            Compare = typeof(TKind) == typeof(ISamplerKind.Comparison) ? CompareFunction.LessEqual : CompareFunction.Undefined,
        };

        resources.Samplers[samplerId] = contextProvider.Value.Device.CreateSampler(descriptor);
        return Result.Success();
    }

    public DeletionResult DeleteSampler(SamplerId samplerId)
    {
        if (!resources.Samplers.Remove(samplerId, out var sampler))
        {
            return DeletionResult.NotFound();
        }

        sampler.Dispose();
        return DeletionResult.Success();
    }
}
