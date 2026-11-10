// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Threading;

namespace ResulTus.Collections.Tests;

/// <summary>
/// Allocation measurement on a dedicated, residue-free thread.
/// </summary>
/// <remarks>
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> only sees the measuring thread, but the test
/// host keeps queues on that thread: residue from previous tests (framework work, async
/// continuations, timers) can land its first bytes inside a measured window and masquerade as
/// library allocation — reproducibly under <c>-c Release</c> scheduling. Running the warmup and the
/// window on a thread created for the measurement starts the account at zero for this thread, so
/// every byte inside the window is attributable to the probe's own operations, whatever the host
/// reuses elsewhere.
/// </remarks>
internal static class AllocationProbe
{
    /// <summary>
    /// Runs <paramref name="warmups"/> then <paramref name="iterations"/> of
    /// <paramref name="operation"/> on a dedicated thread and returns the bytes allocated inside
    /// the measured window.
    /// </summary>
    /// <param name="operation">The probe. Invoked on the dedicated thread only.</param>
    /// <param name="warmups">Calls discarded before the window opens, priming the JIT paths.</param>
    /// <param name="iterations">Measured calls.</param>
    /// <returns>Bytes allocated on the dedicated thread between the two counter reads.</returns>
    public static long Measure(Func<int> operation, int warmups, int iterations)
    {
        long allocated = 0;
        var thread = new Thread(() =>
        {
            var checksum = 0;
            for (var index = 0; index < warmups; index++)
            {
                checksum += operation();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < iterations; index++)
            {
                checksum += operation();
            }

            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(checksum);
        });
        thread.Start();
        thread.Join();
        return allocated;
    }
}
