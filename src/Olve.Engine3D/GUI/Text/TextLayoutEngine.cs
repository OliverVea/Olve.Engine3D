using Olve.Engine3D.GUI.Layout;
using Olve.Engine3D.Assets.Entities;

namespace Olve.Engine3D.GUI.Text;

/// <summary>
/// Computes glyph positions and UV coordinates from font data for text rendering.
/// </summary>
public static class TextLayoutEngine
{
    /// <summary>
    /// Layout data for a single glyph, with position relative to text origin.
    /// </summary>
    public readonly record struct GlyphLayout(
        Vector2 PositionPx,  // Relative to text origin, in pixels
        Vector2 SizePx,       // Glyph size in pixels
        Vector2 UvMin,        // UV min in atlas (normalized 0..1)
        Vector2 UvMax         // UV max in atlas (normalized 0..1)
    );

    /// <summary>
    /// Computes the layout for text with the given font and size.
    /// Returns glyph positions relative to the text origin.
    /// </summary>
    public static IReadOnlyList<GlyphLayout> ComputeLayout(
        string text,
        FontData font,
        float fontSize)
    {
        var glyphs = new List<GlyphLayout>();
        var scale = fontSize / font.Metrics.EmSize;
        var atlasSize = new Vector2(font.Atlas.Size.X, font.Atlas.Size.Y);
        float cursorX = 0f;

        foreach (var rune in text.EnumerateRunes())
        {
            if (!font.GlyphData.TryGetValue(rune, out var glyphData))
            {
                // Fallback for missing glyphs: advance by a quarter em
                cursorX += 0.25f * scale;
                continue;
            }

            if (glyphData.Bounds is { } bounds)
            {
                // Position from PlaneBounds (in em units, scaled to pixels)
                // PlaneBounds.Origin.X is the horizontal bearing (offset from cursor)
                // PlaneBounds.Origin.Y is the vertical offset from baseline
                var x = cursorX + bounds.PlaneBounds.Origin.X * scale;
                var y = bounds.PlaneBounds.Origin.Y * scale;
                var w = bounds.PlaneBounds.Size.X * scale;
                var h = bounds.PlaneBounds.Size.Y * scale;

                // UVs from AtlasBounds (pixel coords → normalized 0..1)
                // Swap Y min/max to flip the glyph vertically (atlas Y=0 at top, but we render top-down)
                var uvMin = new Vector2(
                    bounds.AtlasBounds.Origin.X / atlasSize.X,
                    (bounds.AtlasBounds.Origin.Y + bounds.AtlasBounds.Size.Y) / atlasSize.Y
                );
                var uvMax = new Vector2(
                    (bounds.AtlasBounds.Origin.X + bounds.AtlasBounds.Size.X) / atlasSize.X,
                    bounds.AtlasBounds.Origin.Y / atlasSize.Y
                );

                glyphs.Add(new GlyphLayout(
                    new Vector2(x, y),
                    new Vector2(w, h),
                    uvMin,
                    uvMax
                ));
            }

            cursorX += glyphData.Advance * scale;
        }

        return glyphs;
    }

    /// <summary>
    /// Measures text size without computing full layout.
    /// Returns (width, lineHeight) in pixels.
    /// </summary>
    public static Vector2<Px> MeasureText(string text, FontData font, float fontSize)
    {
        var scale = fontSize / font.Metrics.EmSize;
        var width = 0f;

        foreach (var rune in text.EnumerateRunes())
        {
            if (font.GlyphData.TryGetValue(rune, out var glyphData))
            {
                width += glyphData.Advance * scale;
            }
            else
            {
                width += 0.25f * scale;  // Fallback
            }
        }

        return new Vector2<Px>(
            new Px((int)float.Ceiling(width)),
            new Px((int)float.Ceiling(font.Metrics.LineHeight * scale)));
    }
}
