namespace Olve.Engine3D.Storage;

public sealed class UnavailableFileExchange : IFileExchange
{
    public ValueTask<Result<FileSave>> SaveAsync(string fileName, FileType type, ReadOnlyMemory<byte> bytes) =>
        ValueTask.FromResult<Result<FileSave>>(new ResultProblem("This host can't save files"));

    public ValueTask<Result<FileOpen>> OpenAsync(FileType type) =>
        ValueTask.FromResult<Result<FileOpen>>(new ResultProblem("This host can't open files"));
}
