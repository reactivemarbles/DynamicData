// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Internal;

public sealed class SwappableLockFixture
{
#if NET9_0_OR_GREATER

    [Fact]
    public void CreateAndEnter_AcquiresLock()
    {
        var gate = new Lock();

        using var swappable = SwappableLock.CreateAndEnter(gate);

        gate.IsHeldByCurrentThread.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ReleasesLock()
    {
        var gate = new Lock();
        var swappable = SwappableLock.CreateAndEnter(gate);

        swappable.Dispose();

        gate.IsHeldByCurrentThread.Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var gate = new Lock();
        var swappable = SwappableLock.CreateAndEnter(gate);

        swappable.Dispose();
        swappable.Dispose();

        gate.IsHeldByCurrentThread.Should().BeFalse();
    }

    [Fact]
    public void SwapTo_AcquiresNewAndReleasesOld()
    {
        var first = new Lock();
        var second = new Lock();

        using var swappable = SwappableLock.CreateAndEnter(first);
        swappable.SwapTo(second);

        first.IsHeldByCurrentThread.Should().BeFalse();
        second.IsHeldByCurrentThread.Should().BeTrue();
    }

    [Fact]
    public void SwapTo_ChainedSwaps()
    {
        var a = new Lock();
        var b = new Lock();
        var c = new Lock();

        using var swappable = SwappableLock.CreateAndEnter(a);
        swappable.SwapTo(b);
        swappable.SwapTo(c);

        a.IsHeldByCurrentThread.Should().BeFalse();
        b.IsHeldByCurrentThread.Should().BeFalse();
        c.IsHeldByCurrentThread.Should().BeTrue();
    }

    [Fact]
    public void SwapTo_WithoutCreate_Throws()
    {
        var gate = new Lock();
        var swappable = new SwappableLock();

        try
        {
            swappable.SwapTo(gate);
            throw new Xunit.Sdk.XunitException("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void Dispose_AfterSwap_ReleasesSwappedLock()
    {
        var first = new Lock();
        var second = new Lock();

        var swappable = SwappableLock.CreateAndEnter(first);
        swappable.SwapTo(second);
        swappable.Dispose();

        first.IsHeldByCurrentThread.Should().BeFalse();
        second.IsHeldByCurrentThread.Should().BeFalse();
    }

#else

    [Fact]
    public void CreateAndEnter_AcquiresLock()
    {
        var gate = new object();

        using var swappable = SwappableLock.CreateAndEnter(gate);

        Monitor.IsEntered(gate).Should().BeTrue();
    }

    [Fact]
    public void Dispose_ReleasesLock()
    {
        var gate = new object();
        var swappable = SwappableLock.CreateAndEnter(gate);

        swappable.Dispose();

        Monitor.IsEntered(gate).Should().BeFalse();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var gate = new object();
        var swappable = SwappableLock.CreateAndEnter(gate);

        swappable.Dispose();
        swappable.Dispose();

        Monitor.IsEntered(gate).Should().BeFalse();
    }

    [Fact]
    public void SwapTo_AcquiresNewAndReleasesOld()
    {
        var first = new object();
        var second = new object();

        using var swappable = SwappableLock.CreateAndEnter(first);
        swappable.SwapTo(second);

        Monitor.IsEntered(first).Should().BeFalse();
        Monitor.IsEntered(second).Should().BeTrue();
    }

    [Fact]
    public void SwapTo_ChainedSwaps()
    {
        var a = new object();
        var b = new object();
        var c = new object();

        using var swappable = SwappableLock.CreateAndEnter(a);
        swappable.SwapTo(b);
        swappable.SwapTo(c);

        Monitor.IsEntered(a).Should().BeFalse();
        Monitor.IsEntered(b).Should().BeFalse();
        Monitor.IsEntered(c).Should().BeTrue();
    }

    [Fact]
    public void SwapTo_WithoutCreate_Throws()
    {
        var gate = new object();
        var swappable = new SwappableLock();

        try
        {
            swappable.SwapTo(gate);
            throw new Xunit.Sdk.XunitException("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void Dispose_AfterSwap_ReleasesSwappedLock()
    {
        var first = new object();
        var second = new object();

        var swappable = SwappableLock.CreateAndEnter(first);
        swappable.SwapTo(second);
        swappable.Dispose();

        Monitor.IsEntered(first).Should().BeFalse();
        Monitor.IsEntered(second).Should().BeFalse();
    }

    [Fact]
    public void SwapTo_SameLock_WorksWithReentrantMonitor()
    {
        var gate = new object();

        using var swappable = SwappableLock.CreateAndEnter(gate);
        swappable.SwapTo(gate);

        Monitor.IsEntered(gate).Should().BeTrue();
    }

#endif
}
