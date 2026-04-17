// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive;

using DynamicData.Internal;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Internal;

public class NotificationFixture
{
    [Fact]
    public void CreateNext_WithReferenceType_DeliversOnNext()
    {
        var observer = new RecordingObserver<string>();
        var n = DynamicData.Internal.Notification<string>.CreateNext("hello");

        n.IsTerminal.Should().BeFalse();
        n.Accept(observer);

        observer.NextValue.Should().Be("hello");
        observer.Error.Should().BeNull();
        observer.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void CreateError_WithReferenceType_DeliversOnError()
    {
        var observer = new RecordingObserver<string>();
        var error = new Exception("test");
        var n = DynamicData.Internal.Notification<string>.CreateError(error);

        n.IsTerminal.Should().BeTrue();
        n.IsError.Should().BeTrue();
        n.Accept(observer);

        observer.NextValue.Should().BeNull();
        observer.Error.Should().BeSameAs(error);
        observer.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void CreateCompleted_WithReferenceType_DeliversOnCompleted()
    {
        var observer = new RecordingObserver<string>();
        var n = DynamicData.Internal.Notification<string>.CreateCompleted();

        n.IsTerminal.Should().BeTrue();
        n.IsError.Should().BeFalse();
        n.Accept(observer);

        observer.NextValue.Should().BeNull();
        observer.Error.Should().BeNull();
        observer.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void CreateNext_WithValueType_DeliversOnNext()
    {
        var observer = new RecordingObserver<Unit>();
        var n = DynamicData.Internal.Notification<Unit>.CreateNext(Unit.Default);

        n.IsTerminal.Should().BeFalse();
        n.Accept(observer);

        observer.HasNext.Should().BeTrue();
        observer.Error.Should().BeNull();
        observer.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void CreateError_WithValueType_DeliversOnError()
    {
        var observer = new RecordingObserver<Unit>();
        var error = new Exception("test");
        var n = DynamicData.Internal.Notification<Unit>.CreateError(error);

        n.IsTerminal.Should().BeTrue();
        n.IsError.Should().BeTrue();
        n.Accept(observer);

        observer.HasNext.Should().BeFalse();
        observer.Error.Should().BeSameAs(error);
        observer.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void CreateCompleted_WithValueType_DeliversOnCompleted()
    {
        var observer = new RecordingObserver<Unit>();
        var n = DynamicData.Internal.Notification<Unit>.CreateCompleted();

        n.IsTerminal.Should().BeTrue();
        n.IsError.Should().BeFalse();
        n.Accept(observer);

        observer.HasNext.Should().BeFalse();
        observer.Error.Should().BeNull();
        observer.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void DefaultNotification_WithValueType_IsTerminal()
    {
        // default(DynamicData.Internal.Notification<Unit>) should behave as OnCompleted
        var n = default(DynamicData.Internal.Notification<Unit>);
        n.IsTerminal.Should().BeTrue();
        n.IsError.Should().BeFalse();

        var observer = new RecordingObserver<Unit>();
        n.Accept(observer);
        observer.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void DefaultNotification_WithReferenceType_IsTerminal()
    {
        var n = default(DynamicData.Internal.Notification<string>);
        n.IsTerminal.Should().BeTrue();
        n.IsError.Should().BeFalse();

        var observer = new RecordingObserver<string>();
        n.Accept(observer);
        observer.IsCompleted.Should().BeTrue();
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
