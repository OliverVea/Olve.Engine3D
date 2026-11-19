namespace Olve.Engine3D.Rendering.RenderPasses;

/// <summary>The attachments of one pass; at least one is set.</summary>
public sealed record PassAttachments(ColorAttachment? Color, DepthAttachment? Depth);
