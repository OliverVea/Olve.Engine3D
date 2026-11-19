// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using TraceShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Generated.Shaders.Shaders.Trace.Vertex, Olve.Generated.Shaders.Shaders.Trace.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>;
global using TraceGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Generated.Shaders.Shaders.Trace.Vertex, Olve.Generated.Shaders.Shaders.Trace.Instance>;
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

    public partial class Trace : IShaderDefinition<Trace.Vertex, Trace.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Trace Definition { get; } = new();

        private Trace()
        {
        }


        public readonly record struct Vertex(
            Vector2 position,
            Vector2 stroke
        ) : IVertexData, IWithPosition2D<Vertex>
        {
            public static int FloatCount => 4;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = position.X; buffer[i++] = position.Y;
                buffer[i++] = stroke.X; buffer[i++] = stroke.Y;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 16,
                Instanced: false,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 2, ByteOffset: 0), // position
                    new(Location: 1, ComponentCount: 2, ByteOffset: 8), // stroke
                ]);

            Vector2 IWithPosition2D<Vertex>.Position => position;
            static Vertex IWithPosition2D<Vertex>.WithPosition(Vertex self, Vector2 position)
                => self with { position = position };
        }

        public readonly record struct Instance(
            Vector4 iPhosphor
        ) : IInstanceData
        {
            public static int FloatCount => 4;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iPhosphor.X; buffer[i++] = iPhosphor.Y; buffer[i++] = iPhosphor.Z; buffer[i++] = iPhosphor.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 16,
                Instanced: true,
                Attributes:
                [
                    new(Location: 2, ComponentCount: 4, ByteOffset: 0), // iPhosphor
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Trace",
            Wgsl = new WgslShaderData
            {
                VertexSource = @"struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) stroke_0 : vec2<f32>,
    @location(1) phosphor_0 : vec3<f32>,
};

struct vertexInput_0
{
    @location(0) position_1 : vec2<f32>,
    @location(1) stroke_1 : vec2<f32>,
    @location(2) iPhosphor_0 : vec4<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0) -> VertexOut_0
{
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(_S1.position_1, 0.0f, 1.0f);
    o_0.stroke_0 = _S1.stroke_1;
    o_0.phosphor_0 = _S1.iPhosphor_0.xyz;
    return o_0;
}

",
                FragmentSource = @"struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) stroke_0 : vec2<f32>,
    @location(1) phosphor_0 : vec3<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var o_0 : FragmentOut_0;
    var _S2 : f32 = _S1.stroke_0.x;
    if((_S2 > 1.5f))
    {
        o_0.fragColor_0 = vec4<f32>(0.0f, 0.0f, 0.0f, _S1.stroke_0.y);
        return o_0;
    }
    o_0.fragColor_0 = vec4<f32>(_S1.phosphor_0, _S1.stroke_0.y * exp(-3.5f * _S2 * _S2) * (1.0f - smoothstep(0.85000002384185791f, 1.0f, abs(_S2))));
    return o_0;
}

",
                Pass = new WgslInputsBindings(null, []),
                Group = new WgslInputsBindings(null, []),
            },
        };

        #endregion
    }
}
