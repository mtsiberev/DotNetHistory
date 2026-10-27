using System;
using System.Buffers;
using System.Diagnostics;

namespace DotNetHistory.FeatureExamples;

public class ArrayPoolExample
{
    public string Execute()
    {
        const int Iterations = 100_000;
        const int BufferSize = 4096;

        GC.Collect();
        long memBefore1 = GC.GetTotalAllocatedBytes(true);
        var sw1 = Stopwatch.StartNew();

        for (int i = 0; i < Iterations; i++)
        {
            byte[] buffer = new byte[BufferSize];
            buffer[0] = 1;
        }

        sw1.Stop();
        long alloc1 = GC.GetTotalAllocatedBytes(true) - memBefore1;
        Console.WriteLine($"Standard new byte[]: {sw1.ElapsedMilliseconds} ms, Allocated: {alloc1 / 1024.0 / 1024.0:F2} MB");

        GC.Collect();
        long memBefore2 = GC.GetTotalAllocatedBytes(true);
        var sw2 = Stopwatch.StartNew();
        var pool = ArrayPool<byte>.Shared;

        for (int i = 0; i < Iterations; i++)
        {
            byte[] buffer = pool.Rent(BufferSize);
            buffer[0] = 1;
            pool.Return(buffer);
        }

        sw2.Stop();
        long alloc2 = GC.GetTotalAllocatedBytes(true) - memBefore2;
        Console.WriteLine($"Using ArrayPool:     {sw2.ElapsedMilliseconds} ms, Allocated: {alloc2 / 1024.0 / 1024.0:F2} MB");

        return string.Empty;
    }
}
