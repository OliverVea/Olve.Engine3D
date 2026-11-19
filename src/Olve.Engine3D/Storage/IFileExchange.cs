namespace Olve.Engine3D.Storage;

public interface IFileExchange
{
    ValueTask<Result<FileSave>> SaveAsync(string fileName, FileType type, ReadOnlyMemory<byte> bytes);

    ValueTask<Result<FileOpen>> OpenAsync(FileType type);
}
