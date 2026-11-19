using Olve.WebGPU;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>Copies a texture into a mappable buffer and reads it back, bottom row first.</summary>
internal static class WebGPUTextureReadback
{
    private const int CopyRowAlignment = 256;

    // Copies the texture now and completes once a later frame has mapped the copy.
    // Textures drawn into start at the top row, so their rows are flipped
    public static async Task<Result<byte[]>> ReadAsync(
        WebGPUContext context, Texture texture, int width, int height, int bytesPerPixel, TextureAspect aspect)
    {
        var rowBytes = width * bytesPerPixel;
        var bytesPerRow = (rowBytes + CopyRowAlignment - 1) / CopyRowAlignment * CopyRowAlignment;
        var bufferSize = (ulong)(bytesPerRow * height);
        using var buffer = context.CreateBuffer(BufferUsage.MapRead | BufferUsage.CopyDst, bufferSize);

        using (var encoder = context.Device.CreateCommandEncoder())
        {
            var source = new TexelCopyTextureInfo { Texture = texture, Aspect = aspect };
            var destination = new TexelCopyBufferInfo
            {
                Buffer = buffer,
                Layout = new TexelCopyBufferLayout { BytesPerRow = (uint)bytesPerRow, RowsPerImage = (uint)height },
            };
            encoder.CopyTextureToBuffer(source, destination, new Extent3D { Width = (uint)width, Height = (uint)height });

            using var commands = encoder.Finish();
            context.Queue.Submit([commands]);
        }

        var mapped = await context.AwaitAsync(buffer.MapAsync(MapMode.Read, 0, (nuint)bufferSize));
        if (!mapped.IsSuccess)
        {
            return new ResultProblem("Failed to map the texture's copy ({0}): {1}", mapped.Status, mapped.Message);
        }

        var rows = buffer.GetConstMappedRange(0, (nuint)bufferSize);
        var bytes = new byte[rowBytes * height];
        for (var y = 0; y < height; y++)
        {
            rows.Slice(y * bytesPerRow, rowBytes).CopyTo(bytes.AsSpan((height - 1 - y) * rowBytes, rowBytes));
        }

        buffer.Unmap();
        return bytes;
    }
}
