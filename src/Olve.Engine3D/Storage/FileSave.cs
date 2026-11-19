namespace Olve.Engine3D.Storage;

public abstract record FileSave
{
    private FileSave()
    {
    }

    public sealed record Saved(string Location) : FileSave;

    public sealed record Cancelled : FileSave;
}
