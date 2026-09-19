// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

/// <summary>
/// Regression tests for stale child callbacks after same-key replacement in
/// <see cref="CacheParentSubscription{TParent, TKey, TChild, TObserver}"/>.
/// </summary>
public sealed class CacheParentSubscriptionRegressionFixture
{
    [Test]
    public async Task ErrorHandlerCannotReenterChildDeliveryAfterTermination()
    {
        var failedChild = new ManualObservable<string>();
        var otherChild = new ManualObservable<string>();
        var observer = new TestObserver { ErrorAction = () => otherChild.Emit("after-error") };
        using var subscription = new TestSubscription(observer);
        subscription.AddRawChild(failedChild, 1);
        subscription.AddRawChild(otherChild, 2);
        otherChild.Emit("before-error");

        failedChild.Fail(new InvalidOperationException("first"));
        otherChild.Fail(new InvalidOperationException("second"));

        await Assert.That(observer.ErrorCount).IsEqualTo(1);
        await Assert.That(observer.EmitCount).IsEqualTo(1);
        await Assert.That(subscription.ChildValues).IsEquivalentTo(new[] { "before-error" });
    }

    [Test]
    public async Task SameKeyReplacementIgnoresOldChildCallbacks()
    {
        const int ParentKey = 42;

        var observer = new TestObserver();
        using var subscription = new TestSubscription(observer);
        var oldChild = new DisposalEmittingObservable<string>("old-stale");
        var replacementChild = new ManualObservable<string>();

        subscription.AddRawChild(oldChild, ParentKey);
        oldChild.Emit("old-active");

        subscription.AddRawChild(replacementChild, ParentKey);
        replacementChild.Emit("replacement-active");
        replacementChild.Emit("replacement-after-old-completed");

        await Assert.That(subscription.ChildValues).IsEquivalentTo(
            new[]
            {
                "old-active",
                "replacement-active",
                "replacement-after-old-completed"
            },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(observer.EmitCount).IsEqualTo(3);
    }

    private sealed class TestSubscription(IObserver<string> observer) : CacheParentSubscription<int, int, string, string>(observer)
    {
        private string _pendingChild = string.Empty;
        private bool _hasPendingChild;

        public List<string> ChildValues { get; } = [];

        public void AddRawChild(IObservable<string> observable, int parentKey) =>
            AddChildSubscription(observable, parentKey);

        protected override void ParentOnNext(IChangeSet<int, int> changes)
        {
        }

        protected override void ChildOnNext(string child, int parentKey)
        {
            ChildValues.Add(child);
            _pendingChild = child;
            _hasPendingChild = true;
        }

        protected override void EmitChanges(IObserver<string> observer)
        {
            if (_hasPendingChild)
            {
                _hasPendingChild = false;
                observer.OnNext(_pendingChild);
            }
        }
    }

    private sealed class TestObserver : IObserver<string>
    {
        public int EmitCount { get; private set; }

        public int ErrorCount { get; private set; }

        public Action? ErrorAction { get; init; }

        public void OnNext(string value) => ++EmitCount;

        public void OnError(Exception error)
        {
            ErrorCount++;
            ErrorAction?.Invoke();
        }

        public void OnCompleted()
        {
        }
    }

    private class ManualObservable<T> : IObservable<T>
    {
        private readonly List<IObserver<T>> _observers = [];

        public virtual IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new IgnoredDispose();
        }

        public void Emit(T value)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnNext(value);
            }
        }

        public void Fail(Exception error)
        {
            foreach (var observer in _observers.ToArray())
            {
                observer.OnError(error);
            }
        }
    }

    private sealed class DisposalEmittingObservable<T>(T staleValue) : ManualObservable<T>
    {
        public override IDisposable Subscribe(IObserver<T> observer)
        {
            base.Subscribe(observer);
            return new OnDispose(() =>
            {
                observer.OnNext(staleValue);
                observer.OnCompleted();
            });
        }
    }

    private sealed class IgnoredDispose : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class OnDispose(Action action) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                action();
            }
        }
    }
}
