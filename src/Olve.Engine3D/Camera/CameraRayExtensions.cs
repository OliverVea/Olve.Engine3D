using Olve.Engine3D.Camera.Projections;
using Olve.Engine3D.Camera.Views;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Camera;

public static class CameraRayExtensions
{
    public static Result<Ray> GetRay(this Camera<FirstPersonView, PerspectiveProjection> camera, Vector2 ncdScreenPosition)
    {
        var viewMatrix = camera.GetViewMatrix();
        var projectionMatrix = camera.GetProjectionMatrix();

        var invertResults = Result.Concat(
            () => viewMatrix.Invert().IfProblem(p => p.Prepend("Failed to invert view matrix")),
            () => projectionMatrix.Invert().IfProblem(p => p.Prepend("Failed to invert projection matrix"))
        );

        if (invertResults.TryPickProblems(out var problems, out var invertedMatrices))
        {
            return problems.Prepend("Failed to get ray from screen position '{0}'", ncdScreenPosition);
        }

        var (iViewMatrix, iProjectionMatrix) = invertedMatrices;

        var ndcX = ncdScreenPosition.X;
        var ndcY = ncdScreenPosition.Y;
        var farClip = new Vector4(ndcX, ndcY, 1f, 1f);

        var farView = Vector4.Transform(farClip, iProjectionMatrix);
        farView /= farView.W;

        var farWorld4 = Vector4.Transform(farView, iViewMatrix);
        farWorld4 /= farWorld4.W;
        var farWorld = new Vector3(farWorld4.X, farWorld4.Y, farWorld4.Z);

        var direction = Vector3.Normalize(farWorld - camera.View.Position);

        return new Ray(camera.View.Position, direction);
    }


    public static Result<Ray> GetRay(this Camera<IsometricView, OrthographicProjection> camera, Vector2 ncdScreenPosition)
    {
        if (camera.GetViewMatrix().Invert().TryPickProblems(out var problems, out var invertedViewMatrix))
        {
            return problems.Prepend("Failed to get ray from screen position '{0}'", ncdScreenPosition);
        }

        var invertedRotation = invertedViewMatrix.ExtractRotation();

        var right = Vector3.UnitX;
        var up = -Vector3.UnitY;
        var forward = Vector3.UnitZ;

        right = Vector3.Transform(right, invertedRotation);
        up = Vector3.Transform(up, invertedRotation);
        forward = Vector3.Transform(forward, invertedRotation);

        var halfWidth = camera.Projection.OrthographicSize * camera.Projection.AspectRatio;
        var halfHeight = camera.Projection.OrthographicSize;

        var x = ncdScreenPosition.X * halfWidth;
        var y = ncdScreenPosition.Y * halfHeight;

        var ray = new Ray(camera.View.Position + right * x + up * y, forward);

        // Slide origin along the ray direction so its Y matches the camera Y.
        // This prevents XZ overshoot in heightmap raycasting when the origin
        // is far above terrain (e.g. clicking near the top of the screen).
        return ray.TryEvaluateWithY(camera.View.Position.Y, out var origin)
            ? new Ray(origin, forward)
            : ray;
    }

    /// <summary>Where a world position appears on screen, in normalized coordinates; the inverse of <c>GetRay</c>.</summary>
    public static Result<Vector2> ProjectToNormalized(this Camera<IsometricView, OrthographicProjection> camera, Vector3 world)
    {
        if (camera.GetViewMatrix().Invert().TryPickProblems(out var problems, out var invertedViewMatrix))
        {
            return problems.Prepend("Failed to project world position '{0}'", world);
        }

        var invertedRotation = invertedViewMatrix.ExtractRotation();
        var right = Vector3.Transform(Vector3.UnitX, invertedRotation);
        var up = Vector3.Transform(-Vector3.UnitY, invertedRotation);

        var halfWidth = camera.Projection.OrthographicSize * camera.Projection.AspectRatio;
        var halfHeight = camera.Projection.OrthographicSize;
        if (halfWidth is 0 or float.NaN || halfHeight is 0 or float.NaN)
        {
            return new ResultProblem("Camera projection has invalid dimensions (halfWidth={0}, halfHeight={1})", halfWidth, halfHeight);
        }

        var offset = world - camera.View.Position;
        return new Vector2(Vector3.Dot(offset, right) / halfWidth, Vector3.Dot(offset, up) / halfHeight);
    }
}
