namespace Olve.Engine3D.Assets.Entities;

public readonly record struct FontGlyphBoundsData
{
    public required Rectangle PlaneBounds { get; init; }
    public required Rectangle AtlasBounds { get; init; }
}