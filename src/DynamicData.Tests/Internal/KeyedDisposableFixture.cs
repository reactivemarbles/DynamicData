// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public class KeyedDisposableFixture
{
    [Test]
    public async Task AddTracksDisposable()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        var item = new TestDisposable(() => disposed = true);

        tracker.Add("key", item);

        await Assert.That(tracker.ContainsKey("key")).IsTrue();
        await Assert.That(disposed).IsFalse();
    }

    [Test]
    public async Task RemoveDisposesItem()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        tracker.Add("key", new TestDisposable(() => disposed = true));

        tracker.Remove("key");

        await Assert.That(disposed).IsTrue();
        await Assert.That(tracker.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task AddWithSameKeyDisposePrevious()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed1 = false;
        var disposed2 = false;
        tracker.Add("key", new TestDisposable(() => disposed1 = true));

        tracker.Add("key", new TestDisposable(() => disposed2 = true));

        await Assert.That(disposed1).IsTrue();
        await Assert.That(disposed2).IsFalse();
    }

    [Test]
    public async Task AddWithSameReferenceDoesNotDispose()
    {
        var tracker = new KeyedDisposable<string>();
        var disposeCount = 0;
        var item = new TestDisposable(() => disposeCount++);

        tracker.Add("key", item);
        tracker.Add("key", item); // same reference

        await Assert.That(disposeCount).IsEqualTo(0);
        await Assert.That(tracker.ContainsKey("key")).IsTrue();
    }

    [Test]
    public async Task DisposeDisposesAllItems()
    {
        var tracker = new KeyedDisposable<int>();
        var disposedCount = 0;
        for (var i = 0; i < 5; i++)
            tracker.Add(i, new TestDisposable(() => disposedCount++));

        tracker.Dispose();

        await Assert.That(disposedCount).IsEqualTo(5);
        await Assert.That(tracker.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeIsIdempotent()
    {
        var tracker = new KeyedDisposable<string>();
        var disposeCount = 0;
        tracker.Add("key", new TestDisposable(() => disposeCount++));

        tracker.Dispose();
        tracker.Dispose();

        await Assert.That(disposeCount).IsEqualTo(1);
    }

    [Test]
    public async Task AddAfterDisposeDisposesImmediately()
    {
        var tracker = new KeyedDisposable<string>();
        tracker.Dispose();

        var disposed = false;
        tracker.Add("key", new TestDisposable(() => disposed = true));

        await Assert.That(disposed).IsTrue();
    }

    [Test]
    public async Task DisposeAggregatesExceptions()
    {
        var tracker = new KeyedDisposable<int>();
        tracker.Add(1, new TestDisposable(() => throw new InvalidOperationException("boom1")));
        tracker.Add(2, new TestDisposable(() => { }));
        tracker.Add(3, new TestDisposable(() => throw new InvalidOperationException("boom3")));

        var exception = await Assert.That(() => tracker.Dispose()).Throws<AggregateException>();
        await Assert.That(exception.InnerExceptions).HasCount(2);
        await Assert.That(tracker.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddNonDisposableTracksNothing()
    {
        var tracker = new KeyedDisposable<string>();

        tracker.Add("key", "not disposable");

        await Assert.That(tracker.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task AddNonDisposableRemovesPrevious()
    {
        var tracker = new KeyedDisposable<string>();
        var disposed = false;
        tracker.Add("key", new TestDisposable(() => disposed = true));

        tracker.Add("key", "not disposable");

        await Assert.That(disposed).IsTrue();
        await Assert.That(tracker.ContainsKey("key")).IsFalse();
    }

    [Test]
    public async Task RemoveNonExistentKeyIsNoOp()
    {
        var tracker = new KeyedDisposable<string>();
        tracker.Remove("nonexistent"); // should not throw
    }

    private sealed class TestDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
