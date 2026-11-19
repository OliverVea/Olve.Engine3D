// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using ShapeShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Shape.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.ShapePass, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>;
global using ShapeGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Shape.Instance>;
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

    public partial class Shape : IShaderDefinition<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Shape.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Generated.Shaders.Shaders.Shared.ShapePass, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Shape Definition { get; } = new();

        private Shape()
        {
        }


        public readonly record struct Instance(
            Vector4 iBounds,
            Vector4 iShape,
            Vector4 iExtra,
            Vector4 iColor
        ) : IInstanceData
        {
            public static int FloatCount => 16;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iBounds.X; buffer[i++] = iBounds.Y; buffer[i++] = iBounds.Z; buffer[i++] = iBounds.W;
                buffer[i++] = iShape.X; buffer[i++] = iShape.Y; buffer[i++] = iShape.Z; buffer[i++] = iShape.W;
                buffer[i++] = iExtra.X; buffer[i++] = iExtra.Y; buffer[i++] = iExtra.Z; buffer[i++] = iExtra.W;
                buffer[i++] = iColor.X; buffer[i++] = iColor.Y; buffer[i++] = iColor.Z; buffer[i++] = iColor.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 64,
                Instanced: true,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 4, ByteOffset: 0), // iBounds
                    new(Location: 1, ComponentCount: 4, ByteOffset: 16), // iShape
                    new(Location: 2, ComponentCount: 4, ByteOffset: 32), // iExtra
                    new(Location: 3, ComponentCount: 4, ByteOffset: 48), // iColor
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Shape",
            Wgsl = new WgslShaderData
            {
                VertexSource = @"struct ShapePassData_std140_0
{
    @align(16) uResolution_0 : vec2<f32>,
};

@binding(0) @group(0) var<uniform> shapePass_0 : ShapePassData_std140_0;
struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) bounds_0 : vec4<f32>,
    @location(1) shape_0 : vec4<f32>,
    @location(2) extra_0 : vec4<f32>,
    @location(3) color_0 : vec4<f32>,
};

struct vertexInput_0
{
    @location(0) iBounds_0 : vec4<f32>,
    @location(1) iShape_0 : vec4<f32>,
    @location(2) iExtra_0 : vec4<f32>,
    @location(3) iColor_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    const _S2 : vec2<f32> = vec2<f32>(1.0f, 0.0f);
    const _S3 : vec2<f32> = vec2<f32>(0.0f, 1.0f);
    var corners_0 : array<vec2<f32>, i32(6)> = array<vec2<f32>, i32(6)>( vec2<f32>(0.0f, 0.0f), _S2, _S3, _S3, _S2, vec2<f32>(1.0f, 1.0f) );
    var _S4 : vec2<f32> = vec2<f32>(1.0f);
    var px_0 : vec2<f32> = mix(_S1.iBounds_0.xy - _S4, _S1.iBounds_0.zw + _S4, corners_0[vertexId_0]);
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(px_0.x / shapePass_0.uResolution_0.x * 2.0f - 1.0f, 1.0f - px_0.y / shapePass_0.uResolution_0.y * 2.0f, 0.0f, 1.0f);
    o_0.bounds_0 = _S1.iBounds_0;
    o_0.shape_0 = _S1.iShape_0;
    o_0.extra_0 = _S1.iExtra_0;
    o_0.color_0 = _S1.iColor_0;
    return o_0;
}

",
                FragmentSource = @"fn roundedBox_0( p_0 : vec2<f32>,  halfSize_0 : vec2<f32>,  radius_0 : f32) -> f32
{
    var q_0 : vec2<f32> = abs(p_0) - halfSize_0 + vec2<f32>(radius_0);
    return min(max(q_0.x, q_0.y), 0.0f) + length(max(q_0, vec2<f32>(0.0f))) - radius_0;
}

fn segment_0( p_1 : vec2<f32>,  a_0 : vec2<f32>,  b_0 : vec2<f32>,  halfWidth_0 : f32) -> f32
{
    var pa_0 : vec2<f32> = p_1 - a_0;
    var ba_0 : vec2<f32> = b_0 - a_0;
    return length(pa_0 - ba_0 * vec2<f32>(clamp(dot(pa_0, ba_0) / max(dot(ba_0, ba_0), 9.9999997473787516e-05f), 0.0f, 1.0f))) - halfWidth_0;
}

fn glyphDot_0( low_0 : u32,  high_0 : u32,  x_0 : u32,  y_0 : u32) -> bool
{
    var bit_0 : u32;
    if((y_0 < u32(4)))
    {
        bit_0 = (low_0 >> ((y_0 * u32(5) + x_0)));
    }
    else
    {
        bit_0 = (high_0 >> (((y_0 - u32(4)) * u32(5) + x_0)));
    }
    return ((bit_0 & (u32(1)))) != u32(0);
}

struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) bounds_0 : vec4<f32>,
    @location(1) shape_0 : vec4<f32>,
    @location(2) extra_0 : vec4<f32>,
    @location(3) color_0 : vec4<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var px_0 : vec2<f32> = position_0.xy;
    var _S2 : vec2<f32> = _S1.bounds_0.xy;
    var _S3 : vec2<f32> = _S1.bounds_0.zw;
    var _S4 : vec2<f32> = vec2<f32>(0.5f);
    var _S5 : vec2<f32> = _S3 - _S2;
    var halfSize_1 : vec2<f32> = _S5 * _S4;
    var p_2 : vec2<f32> = px_0 - (_S2 + _S3) * _S4;
    var kind_0 : i32 = i32(_S1.shape_0.x + 0.5f);
    var d_0 : f32;
    if(kind_0 == i32(0))
    {
        var d_1 : f32 = roundedBox_0(p_2, halfSize_1, _S1.shape_0.y);
        var _S6 : f32 = _S1.shape_0.z;
        if((_S6 > 0.0f))
        {
            var _S7 : f32 = _S6 * 0.5f;
            d_0 = abs(d_1 + _S7) - _S7;
        }
        else
        {
            d_0 = d_1;
        }
    }
    else
    {
        if(kind_0 == i32(1))
        {
            var r_0 : f32 = length(p_2);
            var d_2 : f32 = r_0 - halfSize_1.x;
            var _S8 : f32 = _S1.shape_0.y;
            if((_S8 > 0.0f))
            {
                d_0 = max(d_2, _S8 - r_0);
            }
            else
            {
                d_0 = d_2;
            }
        }
        else
        {
            if(kind_0 == i32(2))
            {
                var r_1 : f32 = length(p_2);
                var _S9 : f32 = _S1.extra_0.x;
                var _S10 : f32 = _S1.extra_0.y;
                d_0 = max(max(r_1 - halfSize_1.x, _S1.shape_0.y - r_1), (abs(atan2(p_2.x, - p_2.y) - (_S9 + _S10) * 0.5f) - (_S10 - _S9) * 0.5f) * r_1);
            }
            else
            {
                if(kind_0 == i32(3))
                {
                    d_0 = segment_0(px_0, _S1.extra_0.xy, _S1.extra_0.zw, _S1.shape_0.y * 0.5f);
                }
                else
                {
                    var cell_0 : vec2<f32> = _S5 / vec2<f32>(5.0f, 7.0f);
                    var local_0 : vec2<f32> = px_0 - _S2;
                    var index_0 : vec2<f32> = clamp(floor(local_0 / cell_0), vec2<f32>(0.0f), vec2<f32>(4.0f, 6.0f));
                    if(!glyphDot_0(u32(_S1.shape_0.y + 0.5f), u32(_S1.shape_0.z + 0.5f), u32(index_0.x), u32(index_0.y)))
                    {
                        discard;
                    }
                    d_0 = roundedBox_0(local_0 - (index_0 + _S4) * cell_0, cell_0 * vec2<f32>(0.41999998688697815f), min(cell_0.x, cell_0.y) * 0.18000000715255737f);
                }
            }
        }
    }
    var coverage_0 : f32 = clamp(0.5f - d_0, 0.0f, 1.0f);
    if((coverage_0 <= 0.0f))
    {
        discard;
    }
    var o_0 : FragmentOut_0;
    o_0.fragColor_0 = vec4<f32>(_S1.color_0.xyz, _S1.color_0.w * coverage_0);
    return o_0;
}

",
                Pass = new WgslInputsBindings(0, []),
                Group = new WgslInputsBindings(null, []),
            },
        };

        #endregion
    }
}
