using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Olve.Engine3D.GUI;
using Olve.Engine3D.GUI.Elements;
using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.GUI.Styling;
using Olve.Engine3D.Systems;
using Olve.Engine3D.Time;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Tests.Layout;

public class GuiStyledMountTests
{
    private static readonly GuiElementStyling<Box> ColumnStyle = new()
    {
        StyleKey = new StyleKey("column"),
        OnStateChanged = (box, _) =>
        {
            box.Vertical = true;
            box.Padding = 5;
        },
    };

    [Test]
    public async Task A_Styled_Box_Is_Laid_Out_With_Its_Style_As_Soon_As_It_Is_Mounted()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<EventQueueFactory>();
        services.AddSingleton<DeltaTimeService>();
        services.AddSingleton<IWindowInfo>(new TestWindowInfo());
        services.AddSingleton<IGuiElementStyling>(ColumnStyle);
        services.AddGuiServices();
        services.TryAddScoped<GuiLayoutUpdateService>();
        await using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;

        var layout = sp.GetRequiredService<GuiLayoutService>();
        var layoutUpdate = sp.GetRequiredService<GuiLayoutUpdateService>();
        layout.Load();
        layoutUpdate.Load();

        var anchorId = sp.GetRequiredService<GuiAnchorService>().RegisterAnchor(AnchorPosition.TopLeft, GrowthDirection.DownRight).Value;
        var first = new Box { Id = Id.FromName<GuiElement>("Column/Box/First"), Name = "First", Width = 40, Height = 20 };
        var second = new Box { Id = Id.FromName<GuiElement>("Column/Box/Second"), Name = "Second", Width = 40, Height = 20 };
        var column = new Box { Id = Id.FromName<GuiElement>("Column/Box/Column"), Name = "Column", StyleKey = ColumnStyle.StyleKey, Children = [first, second] };
        var holder = new Box { Id = Id.FromName<GuiElement>("Column/Box/Holder"), Name = "Holder", Children = [column] };

        var elements = sp.GetRequiredService<GuiElementService>();
        var registrationId = elements.RegisterElementAndChildren(anchorId, holder).Value;
        elements.TryGetGuiNodeId(second.Id, registrationId, out var secondNode);

        await Assert.That(layout.TryGetBoxPosition(secondNode, out var position)).IsTrue();
        await Assert.That((position.Position.X.Value, position.Position.Y.Value)).IsEqualTo((5, 25));
    }
}
