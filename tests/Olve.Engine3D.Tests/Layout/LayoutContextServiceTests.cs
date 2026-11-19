using Olve.Engine3D.GUI.Layout;
using System.Numerics;
using Olve.Engine3D.Math;

namespace Olve.Engine3D.Tests.Layout;

public class LayoutContextServiceTests
{
    private sealed class ResizableWindowInfo : IWindowInfo
    {
        public Vector2<int> ContentAreaSize { get; set; } = new(1920, 1080);
    }

    [Test]
    public async Task Design_size_matches_the_content_area()
    {
        var context = new LayoutContextService(new ResizableWindowInfo()).Current;

        await Assert.That(context).IsEqualTo(new LayoutContext(
            new Vector2<Dp>(1920, 1080), 1920f / 1080, new DpPxRatio(1), UiScale: 1));
    }

    [Test]
    public async Task Follows_a_resize()
    {
        var window = new ResizableWindowInfo();
        var service = new LayoutContextService(window);
        _ = service.Current;

        window.ContentAreaSize = new Vector2<int>(1280, 720);

        await Assert.That(service.Current.DesignSize.X).IsEqualTo(new Dp(1280));
    }

    [Test]
    public async Task Minimized_window_keeps_the_last_context()
    {
        var window = new ResizableWindowInfo();
        var service = new LayoutContextService(window);
        var before = service.Current;

        window.ContentAreaSize = new Vector2<int>(0, 0);

        await Assert.That(service.Current).IsEqualTo(before);
    }
}
