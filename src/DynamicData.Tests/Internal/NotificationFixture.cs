#if REACTIVE_TESTS
using DataInternal = DynamicData.Reactive.Internal;
#else
using DataInternal = DynamicData.Internal;
#endif
// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Internal;

public class NotificationFixture
{
    [Test]
    public async Task CreateNext_WithReferenceType_DeliversOnNext()
    {
        var observer = new RecordingObserver<string>();
        var n = DataInternal.Notification<string>.CreateNext("hello");

        await Assert.That(n.IsTerminal).IsFalse();
        n.Accept(observer);

        await Assert.That(observer.NextValue).IsEqualTo("hello");
        await Assert.That(observer.Error).IsNull();
        await Assert.That(observer.IsCompleted).IsFalse();
    }

    [Test]
    public async Task CreateError_WithReferenceType_DeliversOnError()
    {
        var observer = new RecordingObserver<string>();
        var error = new Exception("test");
        var n = DataInternal.Notification<string>.CreateError(error);

        await Assert.That(n.IsTerminal).IsTrue();
        await Assert.That(n.IsError).IsTrue();
        n.Accept(observer);

        await Assert.That(observer.NextValue).IsNull();
        await Assert.That(observer.Error).IsSameReferenceAs(error);
        await Assert.That(observer.IsCompleted).IsFalse();
    }

    [Test]
    public async Task CreateCompleted_WithReferenceType_DeliversOnCompleted()
    {
        var observer = new RecordingObserver<string>();
        var n = DataInternal.Notification<string>.CreateCompleted();

        await Assert.That(n.IsTerminal).IsTrue();
        await Assert.That(n.IsError).IsFalse();
        n.Accept(observer);

        await Assert.That(observer.NextValue).IsNull();
        await Assert.That(observer.Error).IsNull();
        await Assert.That(observer.IsCompleted).IsTrue();
    }

    [Test]
    public async Task CreateNext_WithValueType_DeliversOnNext()
    {
        var observer = new RecordingObserver<Unit>();
        var n = DataInternal.Notification<Unit>.CreateNext(Unit.Default);

        await Assert.That(n.IsTerminal).IsFalse();
        n.Accept(observer);

        await Assert.That(observer.HasNext).IsTrue();
        await Assert.That(observer.Error).IsNull();
        await Assert.That(observer.IsCompleted).IsFalse();
    }

    [Test]
    public async Task CreateError_WithValueType_DeliversOnError()
    {
        var observer = new RecordingObserver<Unit>();
        var error = new Exception("test");
        var n = DataInternal.Notification<Unit>.CreateError(error);

        await Assert.That(n.IsTerminal).IsTrue();
        await Assert.That(n.IsError).IsTrue();
        n.Accept(observer);

        await Assert.That(observer.HasNext).IsFalse();
        await Assert.That(observer.Error).IsSameReferenceAs(error);
        await Assert.That(observer.IsCompleted).IsFalse();
    }

    [Test]
    public async Task CreateCompleted_WithValueType_DeliversOnCompleted()
    {
        var observer = new RecordingObserver<Unit>();
        var n = DataInternal.Notification<Unit>.CreateCompleted();

        await Assert.That(n.IsTerminal).IsTrue();
        await Assert.That(n.IsError).IsFalse();
        n.Accept(observer);

        await Assert.That(observer.HasNext).IsFalse();
        await Assert.That(observer.Error).IsNull();
        await Assert.That(observer.IsCompleted).IsTrue();
    }

    [Test]
    public async Task DefaultNotification_WithValueType_IsTerminal()
    {
        // default(DataInternal.Notification<Unit>) should behave as OnCompleted
        var n = default(DataInternal.Notification<Unit>);
        await Assert.That(n.IsTerminal).IsTrue();
        await Assert.That(n.IsError).IsFalse();

        var observer = new RecordingObserver<Unit>();
        n.Accept(observer);
        await Assert.That(observer.IsCompleted).IsTrue();
    }

    [Test]
    public async Task DefaultNotification_WithReferenceType_IsTerminal()
    {
        var n = default(DataInternal.Notification<string>);
        await Assert.That(n.IsTerminal).IsTrue();
        await Assert.That(n.IsError).IsFalse();

        var observer = new RecordingObserver<string>();
        n.Accept(observer);
        await Assert.That(observer.IsCompleted).IsTrue();
    }

    private sealed class RecordingObserver<T> : IObserver<T>
    {
        public T? NextValue { get; private set; }
        public bool HasNext { get; private set; }
        public Exception? Error { get; private set; }
        public bool IsCompleted { get; private set; }

        public void OnNext(T value) { NextValue = value; HasNext = true; }
        public void OnError(Exception error) => Error = error;
        public void OnCompleted() => IsCompleted = true;
    }
}
