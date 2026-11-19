using Olve.Engine3D.Storage;
using Olve.Paths;
using Olve.Paths.Glob;

namespace Olve.Engine3D.Platform.Desktop;

public sealed class FolderFileExchange(IPath folder) : IFileExchange
{
    public ValueTask<Result<FileSave>> SaveAsync(string fileName, FileType type, ReadOnlyMemory<byte> bytes) =>
        ValueTask.FromResult(Save(fileName, bytes));

    public ValueTask<Result<FileOpen>> OpenAsync(FileType type) =>
        ValueTask.FromResult(Open(type));

    private Result<FileSave> Save(string fileName, ReadOnlyMemory<byte> bytes)
    {
        var file = folder / fileName;
        try
        {
            folder.EnsurePathExists();
            File.WriteAllBytes(file.Path, bytes.Span);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to write '{0}': {1}", file.Path, exception.Message);
        }

        return new FileSave.Saved(file.Path);
    }

    private Result<FileOpen> Open(FileType type)
    {
        var files = folder.TryGlob($"*.{type.Extension}", out var found) ? found.ToList() : [];
        if (files is not [var file])
        {
            return new ResultProblem("'{0}' holds {1} .{2} files; it must hold exactly one to open", folder.Path, files.Count, type.Extension);
        }

        try
        {
            return new FileOpen.Opened(File.ReadAllBytes(file.Path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to read '{0}': {1}", file.Path, exception.Message);
        }
    }
}
