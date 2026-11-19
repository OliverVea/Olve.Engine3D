namespace Olve.Engine3D.Storage;

public abstract record FileOpen
{
    private FileOpen()
    {
    }

    public sealed record Opened(byte[] Bytes) : FileOpen;

    public sealed record Cancelled : FileOpen;
}
