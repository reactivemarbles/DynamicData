// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public sealed class SwappableLockFixture
{
#if NET9_0_OR_GREATER

    [Test]
    public async Task CreateAndEnter_AcquiresLock()
    {
        var gate = new Lock();
        bool isHeld;

        using (SwappableLock.CreateAndEnter(gate))
        {
            isHeld = gate.IsHeldByCurrentThread;
        }

        await Assert.That(isHeld).IsTrue();
    }

    [Test]
    public async Task Dispose_ReleasesLock()
    {
        var gate = new Lock();
        bool isHeld;

        {
            var swappable = SwappableLock.CreateAndEnter(gate);
            swappable.Dispose();

            isHeld = gate.IsHeldByCurrentThread;
        }

        await Assert.That(isHeld).IsFalse();
    }

    [Test]
    public async Task Dispose_IsIdempotent()
    {
        var gate = new Lock();
        bool isHeld;

        {
            var swappable = SwappableLock.CreateAndEnter(gate);
            swappable.Dispose();
            swappable.Dispose();

            isHeld = gate.IsHeldByCurrentThread;
        }

        await Assert.That(isHeld).IsFalse();
    }

    [Test]
    public async Task SwapTo_AcquiresNewAndReleasesOld()
    {
        var first = new Lock();
        var second = new Lock();
        bool firstIsHeld;
        bool secondIsHeld;

        using (var swappable = SwappableLock.CreateAndEnter(first))
        {
            swappable.SwapTo(second);

            firstIsHeld = first.IsHeldByCurrentThread;
            secondIsHeld = second.IsHeldByCurrentThread;
        }

        await Assert.That(firstIsHeld).IsFalse();
        await Assert.That(secondIsHeld).IsTrue();
    }

    [Test]
    public async Task SwapTo_ChainedSwaps()
    {
        var a = new Lock();
        var b = new Lock();
        var c = new Lock();
        bool aIsHeld;
        bool bIsHeld;
        bool cIsHeld;

        using (var swappable = SwappableLock.CreateAndEnter(a))
        {
            swappable.SwapTo(b);
            swappable.SwapTo(c);

            aIsHeld = a.IsHeldByCurrentThread;
            bIsHeld = b.IsHeldByCurrentThread;
            cIsHeld = c.IsHeldByCurrentThread;
        }

        await Assert.That(aIsHeld).IsFalse();
        await Assert.That(bIsHeld).IsFalse();
        await Assert.That(cIsHeld).IsTrue();
    }

    [Test]
    public async Task SwapTo_WithoutCreate_Throws()
    {
        var gate = new Lock();
        var threw = false;

        try
        {
            var swappable = new SwappableLock();
            swappable.SwapTo(gate);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    public async Task Dispose_AfterSwap_ReleasesSwappedLock()
    {
        var first = new Lock();
        var second = new Lock();
        bool firstIsHeld;
        bool secondIsHeld;

        {
            var swappable = SwappableLock.CreateAndEnter(first);
            swappable.SwapTo(second);
            swappable.Dispose();

            firstIsHeld = first.IsHeldByCurrentThread;
            secondIsHeld = second.IsHeldByCurrentThread;
        }

        await Assert.That(firstIsHeld).IsFalse();
        await Assert.That(secondIsHeld).IsFalse();
    }

#else

    [Test]
    public async Task CreateAndEnter_AcquiresLock()
    {
        var gate = new object();

        using var swappable = SwappableLock.CreateAndEnter(gate);

        await Assert.That(Monitor.IsEntered(gate)).IsTrue();
    }

    [Test]
    public async Task Dispose_ReleasesLock()
    {
        var gate = new object();
        var swappable = SwappableLock.CreateAndEnter(gate);

        swappable.Dispose();

        await Assert.That(Monitor.IsEntered(gate)).IsFalse();
    }

    [Test]
    public async Task Dispose_IsIdempotent()
    {
        var gate = new object();
        var swappable = SwappableLock.CreateAndEnter(gate);

        swappable.Dispose();
        swappable.Dispose();

        await Assert.That(Monitor.IsEntered(gate)).IsFalse();
    }

    [Test]
    public async Task SwapTo_AcquiresNewAndReleasesOld()
    {
        var first = new object();
        var second = new object();

        using var swappable = SwappableLock.CreateAndEnter(first);
        swappable.SwapTo(second);

        await Assert.That(Monitor.IsEntered(first)).IsFalse();
        await Assert.That(Monitor.IsEntered(second)).IsTrue();
    }

    [Test]
    public async Task SwapTo_ChainedSwaps()
    {
        var a = new object();
        var b = new object();
        var c = new object();

        using var swappable = SwappableLock.CreateAndEnter(a);
        swappable.SwapTo(b);
        swappable.SwapTo(c);

        await Assert.That(Monitor.IsEntered(a)).IsFalse();
        await Assert.That(Monitor.IsEntered(b)).IsFalse();
        await Assert.That(Monitor.IsEntered(c)).IsTrue();
    }

    [Test]
    public async Task SwapTo_WithoutCreate_Throws()
    {
        var gate = new object();
        var swappable = new SwappableLock();

        await Assert.That(() => swappable.SwapTo(gate)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Dispose_AfterSwap_ReleasesSwappedLock()
    {
        var first = new object();
        var second = new object();

        var swappable = SwappableLock.CreateAndEnter(first);
        swappable.SwapTo(second);
        swappable.Dispose();

        await Assert.That(Monitor.IsEntered(first)).IsFalse();
        await Assert.That(Monitor.IsEntered(second)).IsFalse();
    }

    [Test]
    public async Task SwapTo_SameLock_WorksWithReentrantMonitor()
    {
        var gate = new object();

        using var swappable = SwappableLock.CreateAndEnter(gate);
        swappable.SwapTo(gate);

        await Assert.That(Monitor.IsEntered(gate)).IsTrue();
    }

#endif
}
