namespace Olve.Engine3D.Geometry;

public interface IWithPosition3D<TSelf> where TSelf : IWithPosition3D<TSelf>
{
    Vector3 Position { get; }
    static abstract TSelf WithPosition(TSelf self, Vector3 position);
}

public interface IWithNormal3D<TSelf> where TSelf : IWithNormal3D<TSelf>
{
    Vector3 Normal { get; }
    static abstract TSelf WithNormal(TSelf self, Vector3 normal);
}

public interface IWithTexCoords2D<TSelf> where TSelf : IWithTexCoords2D<TSelf>
{
    Vector2 TexCoords { get; }
    static abstract TSelf WithTexCoords(TSelf self, Vector2 texCoords);
}

public interface IWithColor3D<TSelf> where TSelf : IWithColor3D<TSelf>
{
    Vector3 Color { get; }
    static abstract TSelf WithColor(TSelf self, Vector3 color);
}

public interface IWithPosition2D<TSelf> where TSelf : IWithPosition2D<TSelf>
{
    Vector2 Position { get; }
    static abstract TSelf WithPosition(TSelf self, Vector2 position);
}
