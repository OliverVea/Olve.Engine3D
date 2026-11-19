using System.Buffers.Binary;
using Olve.Engine3D.Diagnostics.Benchmarks;
using Olve.Engine3D.Utilities;
using Olve.WebGPU;
using WgpuBuffer = Olve.WebGPU.Buffer;

namespace Olve.Engine3D.Graphics.WebGPU;

/// <summary>
/// Times a benchmark's measured frames on the GPU, from the start of the frame's first pass to the end of its last,
/// with timestamp queries. Each frame's result is read back a few frames later, through a ring of slots, so timing never
/// waits for the GPU; a frame whose slot is still being read back goes untimed.
/// </summary>
internal sealed class WebGPUGpuTimer(Provider<WebGPUContext> contextProvider, BenchmarkRecorder recorder) : IDisposable
{
    // Enough for the read-back latency at several hundred frames per second
    private const int SlotCount = 64;
    private const ulong TimestampBytes = 2 * sizeof(ulong);

    private readonly Slot?[] _slots = new Slot?[SlotCount];
    private int _next;

    /// <summary>Records the GPU times whose read-back has finished; call after processing events.</summary>
    public void Collect()
    {
        foreach (var slot in _slots)
        {
            slot?.Collect(recorder);
        }
    }

    /// <summary>The slot that times this frame, or null when the frame isn't timed.</summary>
    public Slot? Begin()
    {
        var context = contextProvider.Value;
        if (!context.HasTimestamps || recorder.MeasuredFrame < 0)
        {
            return null;
        }

        var slot = _slots[_next] ??= Slot.Create(context);
        if (slot.IsReading)
        {
            return null;
        }

        _next = (_next + 1) % SlotCount;
        slot.Frame = recorder.MeasuredFrame;
        return slot;
    }

    public void Dispose()
    {
        foreach (var slot in _slots)
        {
            slot?.Dispose();
        }
    }

    internal sealed class Slot(QuerySet querySet, WgpuBuffer resolveBuffer, WgpuBuffer readbackBuffer) : IDisposable
    {
        private Task<BufferMapResult>? _mapping;

        public int Frame { get; set; }

        public bool IsReading => _mapping is not null;

        public static Slot Create(WebGPUContext context) => new(
            context.Device.CreateQuerySet(new QuerySetDescriptor { Type = QueryType.Timestamp, Count = 2 }),
            context.CreateBuffer(BufferUsage.QueryResolve | BufferUsage.CopySrc, TimestampBytes),
            context.CreateBuffer(BufferUsage.MapRead | BufferUsage.CopyDst, TimestampBytes));

        // The first pass writes the frame's start and the last its end; a frame of one pass writes both, and passes in
        // between write nothing
        public PassTimestampWrites? GetWrites(bool isFirst, bool isLast) => !isFirst && !isLast ? null : new PassTimestampWrites
        {
            QuerySet = querySet,
            BeginningOfPassWriteIndex = isFirst ? 0 : Wgpu.QuerySetIndexUndefined,
            EndOfPassWriteIndex = isLast ? 1 : Wgpu.QuerySetIndexUndefined,
        };

        public void Resolve(CommandEncoder encoder)
        {
            encoder.ResolveQuerySet(querySet, 0, 2, resolveBuffer, 0);
            encoder.CopyBufferToBuffer(resolveBuffer, 0, readbackBuffer, 0, TimestampBytes);
        }

        // After the frame's commands are submitted
        public void Read() => _mapping = readbackBuffer.MapAsync(MapMode.Read, 0, (nuint)TimestampBytes);

        public void Collect(BenchmarkRecorder recorder)
        {
            if (_mapping is not { IsCompleted: true } mapping)
            {
                return;
            }

            _mapping = null;
            if (!mapping.Result.IsSuccess)
            {
                return;
            }

            // Timestamps are in nanoseconds
            var timestamps = readbackBuffer.GetConstMappedRange(0, (nuint)TimestampBytes);
            var begin = BinaryPrimitives.ReadUInt64LittleEndian(timestamps);
            var end = BinaryPrimitives.ReadUInt64LittleEndian(timestamps[sizeof(ulong)..]);
            readbackBuffer.Unmap();

            if (end > begin)
            {
                recorder.RecordGpuTime(Frame, (end - begin) / 1_000_000.0);
            }
        }

        public void Dispose()
        {
            querySet.Dispose();
            resolveBuffer.Dispose();
            readbackBuffer.Dispose();
        }
    }
}
