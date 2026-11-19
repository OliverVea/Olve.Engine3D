// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
global using TriangleShaderId = Olve.Engine3D.Rendering.Shaders.ShaderId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Triangle.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>;
global using TriangleGroupId = Olve.Engine3D.Rendering.Instancing.GroupId<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Olve.Generated.Shaders.Shaders.Triangle.Instance>;
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

    public partial class Triangle : IShaderDefinition<Olve.Engine3D.Rendering.Meshes.IVertexData.None, Triangle.Instance, Olve.Engine3D.Rendering.RenderPasses.IColorFormat<Olve.Engine3D.Rendering.Textures.ITextureFormat.Rgba8Unorm>, Olve.Engine3D.Rendering.RenderPasses.IPassInputs.None, Olve.Engine3D.Rendering.Instancing.IGroupInputs.None>
    {
        /// <summary>
        /// This shader's definition, registered with <c>ShaderManager.Register</c>. Definitions are immutable, so each shader has exactly one.
        /// </summary>
        public static Triangle Definition { get; } = new();

        private Triangle()
        {
        }


        public readonly record struct Instance(
            Vector2 iOffset
        ) : IInstanceData
        {
            public static int FloatCount => 2;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = iOffset.X; buffer[i++] = iOffset.Y;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 8,
                Instanced: true,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 2, ByteOffset: 0), // iOffset
                ]);
        }

        #region ShaderSource

        public ShaderData ShaderData { get; } = new()
        {
            Name = "Triangle",
            Wgsl = new WgslShaderData
            {
                VertexSource = @"struct VertexOut_0
{
    @builtin(position) position_0 : vec4<f32>,
    @location(0) color_0 : vec3<f32>,
};

struct vertexInput_0
{
    @location(0) iOffset_0 : vec2<f32>,
};

@vertex
fn vertexMain( _S1 : vertexInput_0, @builtin(vertex_index) vertexId_0 : u32) -> VertexOut_0
{
    var corners_0 : array<vec2<f32>, i32(3)> = array<vec2<f32>, i32(3)>( vec2<f32>(-0.5f, -0.5f), vec2<f32>(0.5f, -0.5f), vec2<f32>(0.0f, 0.5f) );
    var colors_0 : array<vec3<f32>, i32(3)> = array<vec3<f32>, i32(3)>( vec3<f32>(1.0f, 0.0f, 0.0f), vec3<f32>(0.0f, 1.0f, 0.0f), vec3<f32>(0.0f, 0.0f, 1.0f) );
    var o_0 : VertexOut_0;
    o_0.position_0 = vec4<f32>(corners_0[vertexId_0] + _S1.iOffset_0, 0.0f, 1.0f);
    o_0.color_0 = colors_0[vertexId_0];
    return o_0;
}

",
                FragmentSource = @"struct FragmentOut_0
{
    @location(0) fragColor_0 : vec4<f32>,
};

struct pixelInput_0
{
    @location(0) color_0 : vec3<f32>,
};

@fragment
fn fragmentMain( _S1 : pixelInput_0, @builtin(position) position_0 : vec4<f32>) -> FragmentOut_0
{
    var o_0 : FragmentOut_0;
    o_0.fragColor_0 = vec4<f32>(_S1.color_0, 1.0f);
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
