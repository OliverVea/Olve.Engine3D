using Olve.Engine3D.Assets.Entities;
using Olve.Engine3D.Rendering.Textures;

namespace Olve.Engine3D.GUI.Text;

/// <summary>A font the GUI draws with: its glyph metrics and its uploaded atlas.</summary>
public sealed record GuiFont(FontData Data, TextureId<ITextureFormat.Rgba8Unorm, ITextureUsage.Uploaded> Atlas);
