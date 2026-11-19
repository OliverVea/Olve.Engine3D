namespace Olve.Engine3D.Audio;

/// <summary>Writes samples as a 32-bit float WAV file, channels interleaved.</summary>
public static class WavEncoder
{
    private const ushort FloatFormat = 3;
    private const ushort BitsPerSample = 32;

    public static void Write(Stream stream, SampleBuffer buffer, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        var channels = (ushort)buffer.Channels;
        var blockAlign = (ushort)(channels * BitsPerSample / 8);
        var dataBytes = buffer.Frames * blockAlign;

        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write(FloatFormat);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write(blockAlign);
        writer.Write(BitsPerSample);

        writer.Write("data"u8);
        writer.Write(dataBytes);
        for (var frame = 0; frame < buffer.Frames; frame++)
        {
            for (var channel = 0; channel < buffer.Channels; channel++)
            {
                writer.Write(buffer.GetChannel(channel)[frame]);
            }
        }
    }
}
