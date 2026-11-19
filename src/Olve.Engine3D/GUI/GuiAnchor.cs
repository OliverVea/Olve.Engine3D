using Olve.Engine3D.GUI.Layout;
using Olve.Utilities.Ids;
using Olve.Utilities.Lookup;

namespace Olve.Engine3D.GUI;

/// <param name="Offset">Moves a screen anchor's point from <paramref name="Position"/>, e.g. to follow the pointer.</param>
public readonly record struct GuiAnchor(
    Id<GuiAnchor> Id,
    AnchorPosition Position,
    GrowthDirection Growth,
    int Depth = 0,
    Id<GuiNode>? ReferenceNode = null,
    Vector2<Dp> Offset = default) : IHasId<Id<GuiAnchor>>;