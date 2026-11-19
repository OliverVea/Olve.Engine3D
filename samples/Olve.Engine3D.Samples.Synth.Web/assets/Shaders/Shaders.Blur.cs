// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using BlurShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Blur.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Generated.Shaders.Shaders.Blur.Inputs>;
global using BlurGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Blur.Instance>;
// ReSharper disable RedundantUsingDirective
using Olve.Engine3D;
using Olve.Engine3D.Geometry;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.RenderPasses;
using Olve.Utilities.Ids;
using System.Numerics;
using Olve.Engine3D.Math;

// ReSharper disable once CheckNamespace
namespace Olve.Generated.Shaders;


public static partial class Shaders
{

    public partial class Blur : IShaderDefinition<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Blur.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Generated.Shaders.Shaders.Blur.Inputs>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Blur Definition { get; } = new();

        private Blur()
        {
        }

        /// <summary>
        /// The values and textures of one rendering group drawn with this shader.
        /// </summary>
        public readonly record struct Inputs : IGroupInputs
        {
            public required TextureSampler<ITextureFormat.Rgba8Unorm, ISamplerKind.Filtering> USource { get; init; }

            public static int FloatCount => 0;

            public void WriteTo(Span<float> buffer)
            {
            }

            public static int TextureCount => 1;

            public void WriteTextures(Span<TextureBinding> textures)
            {
                textures[0] = new TextureBinding(USource.Texture, USource.Sampler);
            }
        }


        public readonly record struct Instance(
            Vector4 iStep
        ) : IInstanceData
        {
            public static int FloatCount => 4;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iStep.X; buffer[i++] = iStep.Y; buffer[i++] = iStep.Z; buffer[i++] = iStep.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 16,
                Instanced: true,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 4, ByteOffset: 0), // iStep
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Blur",
            Wgsl = new WgslShaderData
            {
                VertexSource = @"struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) uv_0 : vec2<f32>,
    @location(1) step_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) iStep_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    var corners_0 : array<vec2<f32>, i32(3)> = array<vec2<f32>, i32(3)>( vec2<f32>(-1.0f, -1.0f), vec2<f32>(3.0f, -1.0f), vec2<f32>(-1.0f, 3.0f) );
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(corners_0[vertexId_0], 0.0f, 1.0f);
    o_0.uv_0 = vec2<f32>(corners_0[vertexId_0].x * 0.5f + 0.5f, 0.5f - corners_0[vertexId_0].y * 0.5f);
    o_0.step_0 = _S1.iStep_0;
    return o_0;
}

",
                FragmentSource = @"@binding(0) @group(0) var uSource_texture_0 : texture_2d<f32>;

@binding(1) @group(0) var uSource_sampler_0 : sampler;

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) uv_0 : vec2<f32>,
    @location(1) step_0 : vec4<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var weights_0 : array<f32, i32(4)> = array<f32, i32(4)>( 0.19648255407810211f, 0.296906977891922f, 0.094470396637916565f, 0.010381362400949001f );
    var _S2 : array<f32, i32(4)> = array<f32, i32(4)>( 0.0f, 1.4117647409439087f, 3.2941176891326904f, 5.1764707565307617f );
    var width_0 : f32;
    var height_0 : f32;
    {var dim = textureDimensions((uSource_texture_0));((width_0)) = f32(dim.x);((height_0)) = f32(dim.y);};
    var _S3 : vec2<f32> = _S1.step_0.xy / vec2<f32>(height_0);
    ;
    var _S4 : vec3<f32> = (textureSample((uSource_texture_0), (uSource_sampler_0), (_S1.uv_0))).xyz * vec3<f32>(0.19648255407810211f);
    var k_0 : i32 = i32(1);
    var sum_0 : vec3<f32> = _S4;
    loop
    {
        if((k_0 < i32(4)))
        {
        }
        else
        {
            break;
        }
        var _S5 : vec2<f32> = _S3 * vec2<f32>(_S2[k_0]);
        var _S6 : vec2<f32> = _S1.uv_0 + _S5;
        ;
        var _S7 : vec3<f32> = vec3<f32>(weights_0[k_0]);
        var sum_1 : vec3<f32> = sum_0 + (textureSample((uSource_texture_0), (uSource_sampler_0), (_S6))).xyz * _S7;
        var _S8 : vec2<f32> = _S1.uv_0 - _S5;
        ;
        var sum_2 : vec3<f32> = sum_1 + (textureSample((uSource_texture_0), (uSource_sampler_0), (_S8))).xyz * _S7;
        k_0 = k_0 + i32(1);
        sum_0 = sum_2;
    }
    var o_0 : FragmentOut_0;
    o_0.fragColor_0 = vec4<f32>(sum_0, 1.0f);
    return o_0;
}

",
                Pass = new WgslInputsBindings(null, []),
                Group = new WgslInputsBindings(null, [new WgslTextureBinding(0, 1)]),
            },
        };

        #endregion
    }
}
