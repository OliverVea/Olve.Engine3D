// --------------------------------------------------------------------------------------------------
// |                                                                                                |
// |               WARNING: This is a generated file. Do not modify it manually.                    |
// |                                                                                                |
// --------------------------------------------------------------------------------------------------
// ReSharper disable RedundantUsingDirective
using Olve.Engine3D;
using Olve.Engine3D.Geometry;
using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Shaders;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Rendering.Samplers;
using Olve.Engine3D.Rendering.RenderPasses;
using System.Numerics;
using Olve.Engine3D.Math;

// ReSharper disable once CheckNamespace
namespace Olve.Generated.Shaders;

public static partial class Shaders
{
    /// <summary>
    /// Types shared by several shaders, declared with <c>[SharedVertex("Name")]</c>, <c>[PassInputs("Name")]</c> or <c>[GroupInputs("Name")]</c>.
    /// </summary>
    public static class Shared
    {
        public readonly record struct LitVertex(
            Vector3 position,
            Vector3 normal
        ) : IVertexData, IWithPosition3D<LitVertex>, IWithNormal3D<LitVertex>
        {
            public static int FloatCount => 6;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = position.X; buffer[i++] = position.Y; buffer[i++] = position.Z;
                buffer[i++] = normal.X; buffer[i++] = normal.Y; buffer[i++] = normal.Z;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 24,
                Instanced: false,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 3, ByteOffset: 0), // position
                    new(Location: 1, ComponentCount: 3, ByteOffset: 12), // normal
                ]);

            Vector3 IWithPosition3D<LitVertex>.Position => position;
            static LitVertex IWithPosition3D<LitVertex>.WithPosition(LitVertex self, Vector3 position)
                => self with { position = position };

            Vector3 IWithNormal3D<LitVertex>.Normal => normal;
            static LitVertex IWithNormal3D<LitVertex>.WithNormal(LitVertex self, Vector3 normal)
                => self with { normal = normal };
        }

        public readonly record struct RoomVertex(
            Vector3 position,
            Vector3 normal,
            Vector4 albedo,
            Vector4 finish
        ) : IVertexData, IWithPosition3D<RoomVertex>, IWithNormal3D<RoomVertex>
        {
            public static int FloatCount => 14;

            public void WriteTo(Span<float> buffer)
            {
                var i = 0;
                buffer[i++] = position.X; buffer[i++] = position.Y; buffer[i++] = position.Z;
                buffer[i++] = normal.X; buffer[i++] = normal.Y; buffer[i++] = normal.Z;
                buffer[i++] = albedo.X; buffer[i++] = albedo.Y; buffer[i++] = albedo.Z; buffer[i++] = albedo.W;
                buffer[i++] = finish.X; buffer[i++] = finish.Y; buffer[i++] = finish.Z; buffer[i++] = finish.W;
            }

            public static VertexBufferLayout Layout { get; } = new(
                StrideBytes: 56,
                Instanced: false,
                Attributes:
                [
                    new(Location: 0, ComponentCount: 3, ByteOffset: 0), // position
                    new(Location: 1, ComponentCount: 3, ByteOffset: 12), // normal
                    new(Location: 2, ComponentCount: 4, ByteOffset: 24), // albedo
                    new(Location: 3, ComponentCount: 4, ByteOffset: 40), // finish
                ]);

            Vector3 IWithPosition3D<RoomVertex>.Position => position;
            static RoomVertex IWithPosition3D<RoomVertex>.WithPosition(RoomVertex self, Vector3 position)
                => self with { position = position };

            Vector3 IWithNormal3D<RoomVertex>.Normal => normal;
            static RoomVertex IWithNormal3D<RoomVertex>.WithNormal(RoomVertex self, Vector3 normal)
                => self with { normal = normal };
        }

        public readonly record struct ShapePass : IPassInputs
        {
            public Vector2 UResolution { get; init; }

            public static int FloatCount => 4;

            public void WriteTo(Span<float> buffer)
            {
                buffer[0] = UResolution.X; buffer[1] = UResolution.Y;
            }

            public static int TextureCount => 0;

            public void WriteTextures(Span<TextureBinding> textures)
            {
            }
        }

        public readonly record struct SpacePass : IPassInputs
        {
            public Matrix4x4 View { get; init; }
            public Matrix4x4 Projection { get; init; }
            public Vector3 CameraPosition { get; init; }
            public Vector3 SkyAmbient { get; init; }
            public Vector3 GroundAmbient { get; init; }
            public Vector3 KeyPosition { get; init; }
            public Vector3 KeyColor { get; init; }
            public Vector3 Light0Position { get; init; }
            public Vector3 Light0Color { get; init; }
            public Vector3 Light1Position { get; init; }
            public Vector3 Light1Color { get; init; }
            public Vector4 GlowOnly { get; init; }

            public static int FloatCount => 72;

            public void WriteTo(Span<float> buffer)
            {
                buffer[0] = View.M11; buffer[1] = View.M12; buffer[2] = View.M13; buffer[3] = View.M14; buffer[4] = View.M21; buffer[5] = View.M22; buffer[6] = View.M23; buffer[7] = View.M24; buffer[8] = View.M31; buffer[9] = View.M32; buffer[10] = View.M33; buffer[11] = View.M34; buffer[12] = View.M41; buffer[13] = View.M42; buffer[14] = View.M43; buffer[15] = View.M44;
                buffer[16] = Projection.M11; buffer[17] = Projection.M12; buffer[18] = Projection.M13; buffer[19] = Projection.M14; buffer[20] = Projection.M21; buffer[21] = Projection.M22; buffer[22] = Projection.M23; buffer[23] = Projection.M24; buffer[24] = Projection.M31; buffer[25] = Projection.M32; buffer[26] = Projection.M33; buffer[27] = Projection.M34; buffer[28] = Projection.M41; buffer[29] = Projection.M42; buffer[30] = Projection.M43; buffer[31] = Projection.M44;
                buffer[32] = CameraPosition.X; buffer[33] = CameraPosition.Y; buffer[34] = CameraPosition.Z;
                buffer[36] = SkyAmbient.X; buffer[37] = SkyAmbient.Y; buffer[38] = SkyAmbient.Z;
                buffer[40] = GroundAmbient.X; buffer[41] = GroundAmbient.Y; buffer[42] = GroundAmbient.Z;
                buffer[44] = KeyPosition.X; buffer[45] = KeyPosition.Y; buffer[46] = KeyPosition.Z;
                buffer[48] = KeyColor.X; buffer[49] = KeyColor.Y; buffer[50] = KeyColor.Z;
                buffer[52] = Light0Position.X; buffer[53] = Light0Position.Y; buffer[54] = Light0Position.Z;
                buffer[56] = Light0Color.X; buffer[57] = Light0Color.Y; buffer[58] = Light0Color.Z;
                buffer[60] = Light1Position.X; buffer[61] = Light1Position.Y; buffer[62] = Light1Position.Z;
                buffer[64] = Light1Color.X; buffer[65] = Light1Color.Y; buffer[66] = Light1Color.Z;
                buffer[68] = GlowOnly.X; buffer[69] = GlowOnly.Y; buffer[70] = GlowOnly.Z; buffer[71] = GlowOnly.W;
            }

            public static int TextureCount => 0;

            public void WriteTextures(Span<TextureBinding> textures)
            {
            }
        }
    }
}
