using System.Diagnostics.CodeAnalysis;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Rendering.Textures;

/// <summary>
/// Texture id without its format and usage. Equal to any texture id, typed or not, with the same
/// <see cref="Value"/>; <see cref="Format"/> and <see cref="Usage"/> are carried along but not compared.
/// </summary>
public class TextureId : IEquatable<TextureId>
{
    internal TextureId(Id value, Type format, Type usage)
    {
        Value = value;
        Format = format;
        Usage = usage;
    }

    public Id Value { get; }
    public Type Format { get; }
    public Type Usage { get; }

    public static TextureId<TFormat, TUsage> New<TFormat, TUsage>()
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage
        => new(Id.New());

    public bool TryGetAsTypedId<TFormat, TUsage>([MaybeNullWhen(false)] out TextureId<TFormat, TUsage> textureId)
        where TFormat : ITextureFormat
        where TUsage : ITextureUsage
    {
        textureId = typeof(TFormat) == Format && typeof(TUsage) == Usage ? new TextureId<TFormat, TUsage>(Value) : null;
        return textureId is not null;
    }

    public bool Equals(TextureId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is TextureId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => $"{Value} ({Format.Name}, {Usage.Name})";

    public static bool operator ==(TextureId? left, TextureId? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(TextureId? left, TextureId? right) => !(left == right);
}

/// <summary>Id of a <typeparamref name="TFormat"/> texture used as <typeparamref name="TUsage"/>.</summary>
public sealed class TextureId<TFormat, TUsage> : TextureId
    where TFormat : ITextureFormat
    where TUsage : ITextureUsage
{
    internal TextureId(Id value) : base(value, typeof(TFormat), typeof(TUsage))
    {
    }
}
