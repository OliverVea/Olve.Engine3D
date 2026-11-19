namespace Olve.Engine3D.GUI.Layout;

public readonly record struct GuiScrollState(BoxPosition Viewport, UIAxis Axis, Px Offset, Px ContentLength);
