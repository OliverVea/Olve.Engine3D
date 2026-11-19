using Olve.Engine3D.GUI.Text;
using Olve.Engine3D.GUI.Layout;

namespace Olve.Engine3D.GUI.Elements;

public record TextRenderData(
    GuiFont? Font,
    string Content,
    float FontSize,
    float FontWeight,
    RGBA Color,
    Align Align
);