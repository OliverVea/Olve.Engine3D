using Olve.Engine3D.Rendering;
using Olve.Engine3D.Rendering.Instancing;
using Olve.Engine3D.Rendering.Meshes;
using Olve.Engine3D.Rendering.Textures;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Rendering;

public class RenderingIdEqualityTests
{
    private readonly record struct TestVertex : IVertexData
    {
        public void WriteTo(Span<float> buffer) { }
        public static int FloatCount => 0;
        public static VertexBufferLayout Layout => throw new NotSupportedException();
    }

    private readonly record struct TestInstance : IInstanceData
    {
        public void WriteTo(Span<float> buffer) { }
        public static int FloatCount => 0;
        public static VertexBufferLayout Layout => throw new NotSupportedException();
    }

    [Test]
    public async Task Group_Id_Rebuilt_From_Its_Value_Finds_The_Typed_Key()
    {
        var typed = GroupId.New<TestVertex, TestInstance>();
        var groups = new Dictionary<GroupId, string> { [typed] = "group" };

        var untyped = new GroupId(typed.Value);

        await Assert.That(groups.ContainsKey(untyped)).IsTrue();
        await Assert.That(untyped.Equals(typed)).IsTrue();
        await Assert.That(typed.Equals(untyped)).IsTrue();
        await Assert.That(untyped == typed).IsTrue();
    }

    [Test]
    public async Task Mesh_Id_Rebuilt_From_Its_Value_Finds_The_Typed_Key()
    {
        var typed = MeshId.New<TestVertex>();
        var meshes = new Dictionary<MeshId, string> { [typed] = "mesh" };

        var untyped = new MeshId(typed.Value);

        await Assert.That(meshes.ContainsKey(untyped)).IsTrue();
        await Assert.That(typed.Equals(untyped)).IsTrue();
    }

    [Test]
    public async Task Texture_Id_Rebuilt_From_Its_Value_Finds_The_Typed_Key()
    {
        var typed = TextureId.New<ITextureFormat.Rgba8Unorm, ITextureUsage.Uploaded>();
        var textures = new Dictionary<TextureId, string> { [typed] = "texture" };

        var untyped = new TextureId(typed.Value, typeof(ITextureFormat.Rgba8Unorm), typeof(ITextureUsage.Uploaded));

        await Assert.That(textures.ContainsKey(untyped)).IsTrue();
        await Assert.That(typed.Equals(untyped)).IsTrue();
    }

    [Test]
    public async Task Ids_With_Different_Values_Are_Not_Equal()
    {
        var first = GroupId.New<TestVertex, TestInstance>();
        var second = new GroupId(Id.New());

        await Assert.That(first.Equals(second)).IsFalse();
        await Assert.That(first != second).IsTrue();
    }
}
