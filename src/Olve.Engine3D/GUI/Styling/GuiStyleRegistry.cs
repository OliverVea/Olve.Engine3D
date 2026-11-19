using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Olve.Engine3D.GUI.Elements;

namespace Olve.Engine3D.GUI.Styling;

/// <summary>
/// The styles GUI elements can refer to by <see cref="StyleKey"/>: every <see cref="IGuiElementStyling"/> registered as a
/// service, plus any registered at runtime.
/// </summary>
public class GuiStyleRegistry
{
    private readonly ILogger<GuiStyleRegistry> _logger;
    private readonly Dictionary<StyleKey, IGuiElementStyling> _styles = new();

    public GuiStyleRegistry(ILogger<GuiStyleRegistry> logger, IEnumerable<IGuiElementStyling> styles)
    {
        _logger = logger;
        foreach (var styling in styles)
        {
            Register(styling);
        }
    }

    public void Register(IGuiElementStyling styling)
    {
        _styles[styling.StyleKey] = styling;
        _logger.LogDebug("Registered style of type '{TypeName}' and key '{StyleKey}'", styling.GetType().Name, styling.StyleKey.Value);
    }

    public bool Unregister(IGuiElementStyling styling)
    {
        _logger.LogDebug("Unregistering style of type '{TypeName}' and key '{StyleKey}'", styling.GetType().Name, styling.StyleKey.Value);
        return _styles.Remove(styling.StyleKey);
    }

    public bool TryGetStyle(GuiElement guiElement, [MaybeNullWhen(false)] out IGuiElementStyling style)
    {
        style = null;
        return guiElement.StyleKey is {} styleKey && _styles.TryGetValue(styleKey, out style);
    }
}
