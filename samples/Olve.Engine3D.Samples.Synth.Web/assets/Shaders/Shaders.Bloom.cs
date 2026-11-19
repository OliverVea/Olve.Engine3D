// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using BloomShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Bloom.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Generated.Shaders.Shaders.Bloom.Inputs>;
global using BloomGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Bloom.Instance>;
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

    public partial class Bloom : IShaderDefinition<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Bloom.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Generated.Shaders.Shaders.Bloom.Inputs>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Bloom Definition { get; } = new();

        private Bloom()
        {
        }

        /// <summary>
        /// The values and textures of one rendering group drawn with this shader.
        /// </summary>
        public readonly record struct Inputs : IGroupInputs
        {
            public required TextureSampler<ITextureFormat.Rgba8Unorm, ISamplerKind.Filtering> UScene { get; init; }
            public required TextureSampler<ITextureFormat.Rgba8Unorm, ISamplerKind.Filtering> UGlow { get; init; }
            public required TextureSampler<ITextureFormat.Rgba8Unorm, ISamplerKind.Filtering> UBlur { get; init; }

            public static int FloatCount => 0;

            public void WriteTo(Span<float> buffer)
            {
            }

            public static int TextureCount => 3;

            public void WriteTextures(Span<TextureBinding> textures)
            {
                textures[0] = new TextureBinding(UScene.Texture, UScene.Sampler);
                textures[1] = new TextureBinding(UGlow.Texture, UGlow.Sampler);
                textures[2] = new TextureBinding(UBlur.Texture, UBlur.Sampler);
            }
        }


        public readonly record struct Instance(
            Vector4 iStrength
        ) : IInstanceData
        {
            public static int FloatCount => 4;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iStrength.X; buffer[i++] = iStrength.Y; buffer[i++] = iStrength.Z; buffer[i++] = iStrength.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 16,
                Instanced: true,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 4, ByteOffset: 0), // iStrength
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Bloom",
            Wgsl = new WgslShaderData
            {
                VertexSource = @"struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) uv_0 : vec2<f32>,
    @location(1) strength_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) iStrength_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    var corners_0 : array<vec2<f32>, i32(3)> = array<vec2<f32>, i32(3)>( vec2<f32>(-1.0f, -1.0f), vec2<f32>(3.0f, -1.0f), vec2<f32>(-1.0f, 3.0f) );
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(corners_0[vertexId_0], 0.0f, 1.0f);
    o_0.uv_0 = vec2<f32>(corners_0[vertexId_0].x * 0.5f + 0.5f, 0.5f - corners_0[vertexId_0].y * 0.5f);
    o_0.strength_0 = _S1.iStrength_0;
    return o_0;
}

",
                FragmentSource = @"@binding(0) @group(0) var uScene_texture_0 : texture_2d<f32>;

@binding(1) @group(0) var uScene_sampler_0 : sampler;

@binding(4) @group(0) var uBlur_texture_0 : texture_2d<f32>;

@binding(5) @group(0) var uBlur_sampler_0 : sampler;

@binding(2) @group(0) var uGlow_texture_0 : texture_2d<f32>;

@binding(3) @group(0) var uGlow_sampler_0 : sampler;

fn smoothly_0( source_texture_0 : texture_2d<f32>,  source_sampler_0 : sampler,  uv_0 : vec2<f32>) -> vec3<f32>
{
    var width_0 : f32;
    var height_0 : f32;
    {var dim = textureDimensions((source_texture_0));((width_0)) = f32(dim.x);((height_0)) = f32(dim.y);};
    var size_0 : vec2<f32> = vec2<f32>(width_0, height_0);
    var _S1 : vec2<f32> = vec2<f32>(0.5f);
    var texel_0 : vec2<f32> = uv_0 * size_0 - _S1;
    var cell_0 : vec2<f32> = floor(texel_0);
    var f_0 : vec2<f32> = texel_0 - cell_0;
    var _S2 : vec2<f32> = vec2<f32>(1.0f);
    var _S3 : vec2<f32> = _S2 - f_0;
    var _S4 : vec2<f32> = vec2<f32>(6.0f);
    var w0_0 : vec2<f32> = _S3 * _S3 * _S3 / _S4;
    var w1_0 : vec2<f32> = (vec2<f32>(4.0f) - _S4 * f_0 * f_0 + vec2<f32>(3.0f) * f_0 * f_0 * f_0) / _S4;
    var w3_0 : vec2<f32> = f_0 * f_0 * f_0 / _S4;
    var g0_0 : vec2<f32> = w0_0 + w1_0;
    var g1_0 : vec2<f32> = _S2 - w0_0 - w1_0 - w3_0 + w3_0;
    var at0_0 : vec2<f32> = (cell_0 - _S1 + w1_0 / g0_0) / size_0;
    var at1_0 : vec2<f32> = (cell_0 + vec2<f32>(1.5f) + w3_0 / g1_0) / size_0;
    var _S5 : f32 = at0_0.x;
    var _S6 : f32 = at0_0.y;
    var _S7 : vec2<f32> = vec2<f32>(_S5, _S6);
    ;
    var _S8 : vec3<f32> = vec3<f32>(g0_0.x);
    var _S9 : vec3<f32> = (textureSample((source_texture_0), (source_sampler_0), (_S7))).xyz * _S8;
    var _S10 : f32 = at1_0.x;
    var _S11 : vec2<f32> = vec2<f32>(_S10, _S6);
    ;
    var _S12 : vec3<f32> = vec3<f32>(g1_0.x);
    var _S13 : vec3<f32> = (_S9 + (textureSample((source_texture_0), (source_sampler_0), (_S11))).xyz * _S12) * vec3<f32>(g0_0.y);
    var _S14 : f32 = at1_0.y;
    var _S15 : vec2<f32> = vec2<f32>(_S5, _S14);
    ;
    var _S16 : vec3<f32> = (textureSample((source_texture_0), (source_sampler_0), (_S15))).xyz * _S8;
    var _S17 : vec2<f32> = vec2<f32>(_S10, _S14);
    ;
    return _S13 + (_S16 + (textureSample((source_texture_0), (source_sampler_0), (_S17))).xyz * _S12) * vec3<f32>(g1_0.y);
}

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) uv_1 : vec2<f32>,
    @location(1) strength_0 : vec4<f32>,
};

@fragment
fn fragmentMain( _S18 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var fromMiddle_0 : vec2<f32> = _S18.uv_1 - vec2<f32>(0.5f);
    var edge_0 : f32 = dot(fromMiddle_0, fromMiddle_0) * 2.0f;
    var fringe_0 : vec2<f32> = fromMiddle_0 * vec2<f32>(edge_0) * vec2<f32>(_S18.strength_0.w);
    var _S19 : vec2<f32> = _S18.uv_1 + fringe_0;
    ;
    var _S20 : f32 = (textureSample((uScene_texture_0), (uScene_sampler_0), (_S19))).x;
    ;
    var _S21 : f32 = (textureSample((uScene_texture_0), (uScene_sampler_0), (_S18.uv_1))).y;
    var _S22 : vec2<f32> = _S18.uv_1 - fringe_0;
    ;
    var o_0 : FragmentOut_0;
    o_0.fragColor_0 = vec4<f32>(min((vec3<f32>(_S20, _S21, (textureSample((uScene_texture_0), (uScene_sampler_0), (_S22))).z) + smoothly_0(uBlur_texture_0, uBlur_sampler_0, _S18.uv_1) * vec3<f32>(_S18.strength_0.x) + smoothly_0(uGlow_texture_0, uGlow_sampler_0, _S18.uv_1) * vec3<f32>(_S18.strength_0.y)) * vec3<f32>((1.0f - _S18.strength_0.z * smoothstep(0.15000000596046448f, 0.94999998807907104f, edge_0))), vec3<f32>(1.0f)), 1.0f);
    return o_0;
}

",
                Pass = new WgslInputsBindings(null, []),
                Group = new WgslInputsBindings(null, [new WgslTextureBinding(0, 1), new WgslTextureBinding(2, 3), new WgslTextureBinding(4, 5)]),
            },
        };

        #endregion
    }
}
