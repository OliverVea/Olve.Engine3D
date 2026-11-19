using Olve.Engine3D;
using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests;

internal sealed class TestWindowInfo : IWindowInfo
{
    public Vector2<int> ContentAreaSize { get; init; } = new(1920, 1080);
}
