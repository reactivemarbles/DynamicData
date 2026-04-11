// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Internal;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Internal;

public class KeyedDisposableFixture
{
    [Fact]
    public void AddTracksDisposable()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        var item = new TestDisposable(() => disposed = true);

        tracker.Add("key", item);

        tracker.ContainsKey("key").Should().BeTrue();
        disposed.Should().BeFalse();
    }

    [Fact]
    public void RemoveDisposesItem()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        tracker.Add("key", new TestDisposable(() => disposed = true));

        tracker.Remove("key");

        disposed.Should().BeTrue();
        tracker.ContainsKey("key").Should().BeFalse();
    }

    [Fact]
    public void AddWithSameKeyDisposePrevious()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed1 = false;
        var disposed2 = false;
        tracker.Add("key", new TestDisposable(() => disposed1 = true));

        tracker.Add("key", new TestDisposable(() => disposed2 = true));

        disposed1.Should().BeTrue("previous item should be disposed");
        disposed2.Should().BeFalse("new item should not be disposed");
    }

    [Fact]
    public void AddWithSameReferenceDoesNotDispose()
    {
        var tracker = new KeyedDisposable<string>();
        var disposeCount = 0;
        var item = new TestDisposable(() => disposeCount++);

        tracker.Add("key", item);
        tracker.Add("key", item); // same reference

        disposeCount.Should().Be(0, "same reference should not be disposed");
        tracker.ContainsKey("key").Should().BeTrue();
    }

    [Fact]
    public void DisposeDisposesAllItems()
    {
        var tracker = new KeyedDisposable<int>();
        var disposedCount = 0;
        for (var i = 0; i < 5; i++)
            tracker.Add(i, new TestDisposable(() => disposedCount++));

        tracker.Dispose();

        disposedCount.Should().Be(5);
        tracker.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var tracker = new KeyedDisposable<string>();
        var disposeCount = 0;
        tracker.Add("key", new TestDisposable(() => disposeCount++));

        tracker.Dispose();
        tracker.Dispose();

        disposeCount.Should().Be(1);
    }

    [Fact]
    public void AddAfterDisposeDisposesImmediately()
    {
        var tracker = new KeyedDisposable<string>();
        tracker.Dispose();

        var disposed = false;
        tracker.Add("key", new TestDisposable(() => disposed = true));

        disposed.Should().BeTrue("item added after Dispose should be disposed immediately");
    }

    [Fact]
    public void DisposeAggregatesExceptions()
    {
        var tracker = new KeyedDisposable<int>();
        tracker.Add(1, new TestDisposable(() => throw new InvalidOperationException("boom1")));
        tracker.Add(2, new TestDisposable(() => { }));
        tracker.Add(3, new TestDisposable(() => throw new InvalidOperationException("boom3")));

        var act = () => tracker.Dispose();

        act.Should().Throw<AggregateException>()
            .Which.InnerExceptions.Should().HaveCount(2);
        tracker.Count.Should().Be(0, "all items should be cleared even after exceptions");
    }

    [Fact]
    public void AddIfDisposableTracksDisposableItem()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        var item = new TestDisposable(() => disposed = true);

        tracker.AddIfDisposable("key", item);

        tracker.ContainsKey("key").Should().BeTrue();

        tracker.Remove("key");
        disposed.Should().BeTrue();
    }

    [Fact]
    public void AddIfDisposableIgnoresNonDisposableItem()
    {
        var tracker = new KeyedDisposable<string>();

        tracker.AddIfDisposable("key", "not disposable");

        tracker.ContainsKey("key").Should().BeFalse();
    }

    [Fact]
    public void AddIfDisposableRemovesPreviousWhenNewIsNotDisposable()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        tracker.Add("key", new TestDisposable(() => disposed = true));

        tracker.AddIfDisposable("key", "not disposable");

        disposed.Should().BeTrue("previous disposable should be disposed");
        tracker.ContainsKey("key").Should().BeFalse();
    }

    [Fact]
    public void RemoveNonExistentKeyIsNoOp()
    {
        var tracker = new KeyedDisposable<string>();
        tracker.Remove("nonexistent"); // should not throw
    }

    private sealed class TestDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}