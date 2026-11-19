namespace Olve.Engine3D.Math;

public static class RectangleExtensions
{
    extension(in Rectangle rectangle)
    {
        /// <summary>Whether the point is inside, edges included.</summary>
        public bool Contains(Vector2 point) =>
            point.X >= rectangle.Origin.X && point.Y >= rectangle.Origin.Y &&
            point.X <= rectangle.Max.X && point.Y <= rectangle.Max.Y;
    }
}
