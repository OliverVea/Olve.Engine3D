namespace Olve.Engine3D.Assets;

public readonly record struct AssetPath<T>
{
    public string Name { get; }
    public IPurePath Path { get; }

    public AssetPath(string name, string path)
    {
        Name = name;
        Path = Paths.Path.CreatePure(path, PathPlatform.Unix);
    }

    public static implicit operator string(AssetPath<T> path) => path.Path.Path;
}