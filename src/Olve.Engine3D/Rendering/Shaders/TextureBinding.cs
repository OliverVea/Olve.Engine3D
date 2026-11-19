using Olve.Engine3D.Rendering.Textures;
using Olve.Engine3D.Rendering.Samplers;

namespace Olve.Engine3D.Rendering.Shaders;

/// <summary>
/// One texture of an inputs value, as the renderer binds it: the texture, and its sampler unless the shader reads it without one.
/// </summary>
public readonly record struct TextureBinding(TextureId? Texture, SamplerId? Sampler);
