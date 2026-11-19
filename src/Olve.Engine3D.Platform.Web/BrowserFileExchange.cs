using System.Runtime.InteropServices.JavaScript;
using Olve.Engine3D.Storage;

namespace Olve.Engine3D.Platform.Web;

public sealed partial class BrowserFileExchange(Uri moduleUri) : IFileExchange
{
    private const string ModuleName = "Olve.Engine3D.Platform.Web.FileExchange";

    private Task<JSObject>? _module;

    public async ValueTask<Result<FileSave>> SaveAsync(string fileName, FileType type, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            await LoadModuleAsync();
            Save(fileName, new ArraySegment<byte>(bytes.ToArray()));
            return new FileSave.Saved($"a download named '{fileName}'");
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to offer '{0}' as a download", fileName);
        }
    }

    public async ValueTask<Result<FileOpen>> OpenAsync(FileType type)
    {
        try
        {
            await LoadModuleAsync();
            using var file = await Open($".{type.Extension}");
            if (file is null)
            {
                return new FileOpen.Cancelled();
            }

            var bytes = new byte[Length(file)];
            Copy(file, new ArraySegment<byte>(bytes));
            return new FileOpen.Opened(bytes);
        }
        catch (JSException exception)
        {
            return new ResultProblem(exception, "Failed to open a file");
        }
    }

    private Task<JSObject> LoadModuleAsync() =>
        _module ??= JSHost.ImportAsync(ModuleName, moduleUri.ToString());

    [JSImport("save", ModuleName)]
    private static partial void Save(string name, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes);

    [JSImport("open", ModuleName)]
    private static partial Task<JSObject?> Open(string accept);

    [JSImport("length", ModuleName)]
    private static partial int Length(JSObject bytes);

    [JSImport("copy", ModuleName)]
    private static partial void Copy(JSObject source, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> target);
}
