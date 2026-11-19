using NativeFileDialogNET;
using Olve.Engine3D.Storage;

namespace Olve.Engine3D.Platform.Desktop;

public sealed class NativeDialogFileExchange : IFileExchange
{
    public ValueTask<Result<FileSave>> SaveAsync(string fileName, FileType type, ReadOnlyMemory<byte> bytes) =>
        ValueTask.FromResult(Save(fileName, type, bytes));

    public ValueTask<Result<FileOpen>> OpenAsync(FileType type) =>
        ValueTask.FromResult(Open(type));

    private static Result<FileSave> Save(string fileName, FileType type, ReadOnlyMemory<byte> bytes)
    {
        using var dialog = new NativeFileDialog().SaveFile().AddFilter(type.Description, type.Extension);
        var result = dialog.Open(out string? path, StartFolder(), fileName);
        if (result == DialogResult.Cancel || path is null)
        {
            return new FileSave.Cancelled();
        }

        if (result != DialogResult.Okay)
        {
            return new ResultProblem("The save dialog failed");
        }

        try
        {
            File.WriteAllBytes(path, bytes.Span);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to write '{0}': {1}", path, exception.Message);
        }

        return new FileSave.Saved(path);
    }

    private static Result<FileOpen> Open(FileType type)
    {
        using var dialog = new NativeFileDialog().SelectFile().AddFilter(type.Description, type.Extension);
        var result = dialog.Open(out string[]? paths, StartFolder());
        if (result == DialogResult.Cancel || paths is not [var path, ..])
        {
            return new FileOpen.Cancelled();
        }

        if (result != DialogResult.Okay)
        {
            return new ResultProblem("The open dialog failed");
        }

        try
        {
            return new FileOpen.Opened(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ResultProblem("Failed to read '{0}': {1}", path, exception.Message);
        }
    }

    private static string StartFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Directory.Exists(documents) ? documents : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
