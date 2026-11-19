using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.GUI;
using Olve.Engine3D.GUI.Collision;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Math;
using Olve.Results.TUnit;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Layout;

public class GuiScrollBoxTests
{
    private const float RowHeight = 40f;
    private const float ViewHeight = 100f;

    private readonly GuiNodeService _nodes = new(NullLogger<GuiNodeService>.Instance);
    private readonly GuiLayoutService _layout;
    private readonly Id<GuiAnchor> _anchorId;

    public GuiScrollBoxTests()
    {
        var anchors = new GuiAnchorService(NullLogger<GuiAnchorService>.Instance);
        _anchorId = anchors.RegisterAnchor(AnchorPosition.TopLeft, GrowthDirection.DownRight).Value;
        var elements = new GuiElementService(_nodes);
        var states = new GuiNodeStateService(NullLogger<GuiNodeStateService>.Instance, _nodes, elements);
        _layout = new GuiLayoutService(NullLogger<GuiLayoutService>.Instance, _nodes, states, anchors, new LayoutContextService(new TestWindowInfo()));
        _layout.Load();
    }

    private async Task<(Id<GuiNode> List, List<Id<GuiNode>> Rows)> BuildList(int rowCount)
    {
        var holder = await _nodes.AddNode("Holder", _anchorId).AssertSuccessAndGetAsync();
        _layout.SetNodeBox(holder, new LayoutBox { LayoutAxis = UIAxis.Y });
        var list = await _nodes.AddNode("List", holder).AssertSuccessAndGetAsync();
        _layout.SetNodeBox(list, new LayoutBox
        {
            LayoutAxis = UIAxis.Y,
            Scroll = true,
            Size = new SizeSpec(PreferredWidth: 100, ResizingWeight: 0f, MaxHeight: ViewHeight),
        });

        var rows = new List<Id<GuiNode>>();
        for (var i = 0; i < rowCount; i++)
        {
            var row = await _nodes.AddNode($"Row{i}", list).AssertSuccessAndGetAsync();
            _layout.SetNodeBox(row, new LayoutBox { Size = new SizeSpec(PreferredWidth: 100, PreferredHeight: RowHeight) });
            rows.Add(row);
        }

        return (list, rows);
    }

    private float TopOf(Id<GuiNode> node) =>
        _layout.TryGetBoxPosition(node, out var position) ? position.Position.Y.Value : float.NaN;

    [Test]
    public async Task A_Scroll_Box_Grows_With_Its_Content_Up_To_Its_Maximum_And_Clips_Its_Rows_To_It()
    {
        var (shortList, _) = await BuildList(2);
        await Assert.That(_layout.TryGetBoxPosition(shortList, out var shortBox)).IsTrue();
        await Assert.That(shortBox.Size.Y.Value).IsEqualTo(2 * RowHeight);
        _ = _nodes.RemoveNode(shortList);

        var (list, rows) = await BuildList(5);

        await Assert.That(_layout.TryGetBoxPosition(list, out var box)).IsTrue();
        await Assert.That(box.Size.Y.Value).IsEqualTo(ViewHeight);
        await Assert.That(rows.Select(TopOf)).IsEquivalentTo([0f, 40f, 80f, 120f, 160f]);
        await Assert.That(_layout.TryGetClip(rows[4], out var clip)).IsTrue();
        await Assert.That(clip).IsEqualTo(new BoxPosition(new Vector2<Px>(new Px(0), new Px(0)), new Vector2<Px>(new Px(100), new Px(100))));
        await Assert.That(_layout.TryGetClip(list, out _)).IsFalse();
    }

    [Test]
    public async Task A_Clipping_Box_Clips_Its_Content_Within_Its_Parents_Clip()
    {
        var (_, rows) = await BuildList(5);
        var frame = await _nodes.AddNode("Frame", rows[1]).AssertSuccessAndGetAsync();
        _layout.SetNodeBox(rows[1], new LayoutBox { Clip = true, Size = new SizeSpec(PreferredWidth: 50, PreferredHeight: RowHeight) });
        _layout.SetNodeBox(frame, new LayoutBox { Size = new SizeSpec(PreferredWidth: 100, PreferredHeight: 100) });

        await Assert.That(_layout.TryGetClip(frame, out var clip)).IsTrue();
        await Assert.That(clip).IsEqualTo(new BoxPosition(new Vector2<Px>(new Px(0), new Px(40)), new Vector2<Px>(new Px(50), new Px(40))));
    }

    [Test]
    [Arguments(new[] { 30f }, 30f)]
    [Arguments(new[] { 1000f }, 100f)]
    [Arguments(new[] { 1000f, -1000f }, 0f)]
    [Arguments(new[] { -20f }, 0f)]
    public async Task Scrolling_Moves_The_Content_And_Stops_At_Its_Ends(float[] scrolls, float expectedOffset)
    {
        var (list, rows) = await BuildList(5);

        foreach (var scroll in scrolls)
        {
            _layout.ScrollBy(list, scroll);
        }

        await Assert.That(TopOf(rows[0])).IsEqualTo(-expectedOffset);
        await Assert.That(_layout.TryGetScrollState(list, out var state)).IsTrue();
        await Assert.That(state).IsEqualTo(new GuiScrollState(
            new BoxPosition(new Vector2<Px>(new Px(0), new Px(0)), new Vector2<Px>(new Px(100), new Px(100))),
            UIAxis.Y, new Px((int)expectedOffset), new Px(5 * (int)RowHeight)));
    }

    [Test]
    public async Task Removing_Rows_Pulls_A_List_Back_And_Putting_Them_Back_Keeps_Its_Scroll()
    {
        var (list, rows) = await BuildList(5);
        _layout.ScrollBy(list, 1000);
        await Assert.That(TopOf(rows[0])).IsEqualTo(-100f);

        _ = _nodes.RemoveNode(rows[4]);
        _ = _nodes.RemoveNode(rows[3]);

        await Assert.That(TopOf(rows[0])).IsEqualTo(-20f);

        foreach (var i in (int[])[3, 4])
        {
            var row = await _nodes.AddNode($"Row{i}", list).AssertSuccessAndGetAsync();
            _layout.SetNodeBox(row, new LayoutBox { Size = new SizeSpec(PreferredWidth: 100, PreferredHeight: RowHeight) });
        }

        await Assert.That(TopOf(rows[0])).IsEqualTo(-100f);
    }

    [Test]
    public async Task Scrolling_A_Row_Into_View_Scrolls_The_Least_That_Shows_All_Of_It()
    {
        var (_, rows) = await BuildList(5);

        _layout.ScrollIntoView(rows[3]);
        await Assert.That(TopOf(rows[0])).IsEqualTo(-60f);

        _layout.ScrollIntoView(rows[2]);
        await Assert.That(TopOf(rows[0])).IsEqualTo(-60f);

        _layout.ScrollIntoView(rows[0]);
        await Assert.That(TopOf(rows[0])).IsEqualTo(0f);
    }

    [Test]
    public async Task The_Pointer_Hits_Only_The_Visible_Part_Of_A_List()
    {
        var (list, rows) = await BuildList(5);
        var collisions = new GuiCollisionService(_layout);

        await Assert.That(collisions.GetGuiCollisions(new Vector2<Px>(new Px(50), new Px(130)))).DoesNotContain(rows[3]);

        _layout.ScrollBy(list, 100);

        await Assert.That(collisions.GetGuiCollisions(new Vector2<Px>(new Px(50), new Px(30))))
            .Contains(rows[3]).And.DoesNotContain(rows[0]);
    }
}
