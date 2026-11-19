using System.IO.Pipes;
using MemoryPack;
using Olve.Engine3D.Commands;
using Microsoft.Extensions.Logging;

namespace Olve.Engine3D.Platform.Desktop.Commands;

/// <summary>Queues the commands that <see cref="CommandPipeClient"/> sends over this game instance's named pipe.</summary>
public class CommandPipeServer(
    GameInstanceId instanceId,
    CommandQueue commandQueue,
    ILogger<CommandPipeServer> logger) : ICommandSource
{
    private const int ListenerCount = 2;

    private readonly CancellationTokenSource _cts = new();
    private Task? _listenTask;

    public void Start()
    {
        logger.LogInformation("Starting command pipe server on pipe '{PipeName}'", instanceId.GetPipeName());
        _listenTask = Task.WhenAll(Enumerable.Range(0, ListenerCount).Select(_ => Task.Run(async () => await ListenLoop(_cts.Token))));
    }

    // Clients are served concurrently: an asynchronous command replies only after the steps sent after it have run
    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    instanceId.GetPipeName(),
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                _ = ServeClient(pipe, ct);
                pipe = null;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in pipe listen loop");
                await Task.Delay(100, ct);
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    private async Task ServeClient(NamedPipeServerStream pipe, CancellationToken ct)
    {
        await using (pipe)
        {
            try
            {
                await HandleClient(pipe, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error handling pipe client");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task HandleClient(NamedPipeServerStream pipe, CancellationToken ct)
    {
        var requestBytes = await ReadLengthPrefixed(pipe, ct);
        var request = MemoryPackSerializer.Deserialize<CommandRequest>(requestBytes);

        if (request is null)
        {
            logger.LogWarning("Received null command request");
            return;
        }

        logger.LogDebug("Received command: '{Command}'", request.Command);

        var tcs = new TaskCompletionSource<CommandResponse>();
        commandQueue.Enqueue(request.Command, tcs);

        var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);

        var responseBytes = MemoryPackSerializer.Serialize(response);
        await WriteLengthPrefixed(pipe, responseBytes, ct);
    }

    private static async Task<byte[]> ReadLengthPrefixed(Stream stream, CancellationToken ct)
    {
        var lengthBytes = new byte[4];
        await stream.ReadExactlyAsync(lengthBytes, ct);
        var length = BitConverter.ToInt32(lengthBytes);

        var data = new byte[length];
        await stream.ReadExactlyAsync(data, ct);
        return data;
    }

    private static async Task WriteLengthPrefixed(Stream stream, byte[] data, CancellationToken ct)
    {
        var lengthBytes = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(lengthBytes, ct);
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore cancellation exceptions during shutdown
        }

        try
        {
            _cts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }
}
