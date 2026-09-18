// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using DynamicData.Internal;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// Focused behavioural tests for <see cref="SynchronizeSafeExtensions.UnsynchronizedCombineLatest{TFirst, TSecond, TResult}(IObservable{TFirst}, IObservable{TSecond}, Func{TFirst, TSecond, TResult})"/>.
/// Covers the contract the helper has to honour as a drop-in <see cref="System.Reactive.Linq.Observable.CombineLatest{TFirst, TSecond, TResult}(IObservable{TFirst}, IObservable{TSecond}, Func{TFirst, TSecond, TResult})"/>
/// replacement: emits only after both sources have produced at least one value, then on every subsequent OnNext from either side; first error terminates;
/// completes only after both sources complete.
/// </summary>
public sealed class UnsynchronizedCombineLatestFixture
{
    [Fact]
    public void OnNext_DoesNotEmit_UntilBothSourcesHaveProduced()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        var received = new List<string>();
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(received.Add);

        a.OnNext(1);
        received.Should().BeEmpty("only the first source has produced");

        a.OnNext(2);
        received.Should().BeEmpty("the second source still has not produced");

        b.OnNext("first");
        received.Should().Equal("2:first");
    }

    [Fact]
    public void OnNext_AfterBothHaveProduced_EmitsOnEverySubsequentValue()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        var received = new List<string>();
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(received.Add);

        a.OnNext(1);
        b.OnNext("x");
        a.OnNext(2);
        b.OnNext("y");
        b.OnNext("z");
        a.OnNext(3);

        received.Should().Equal("1:x", "2:x", "2:y", "2:z", "3:z");
    }

    [Fact]
    public void OnCompleted_FiresOnlyAfterBothSourcesComplete()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        var completed = false;
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(_ => { }, () => completed = true);

        a.OnCompleted();
        completed.Should().BeFalse("the second source is still live");

        b.OnCompleted();
        completed.Should().BeTrue();
    }

    [Fact]
    public void OnError_FromAnySource_TerminatesImmediately()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        Exception? captured = null;
        var completed = false;
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(_ => { }, e => captured = e, () => completed = true);

        var error = new InvalidOperationException("first");
        b.OnError(error);

        captured.Should().BeSameAs(error);
        completed.Should().BeFalse("OnCompleted must not fire after OnError");
    }

    [Fact]
    public void OnError_AfterFirstError_IsIgnored()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        Exception? captured = null;
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(_ => { }, e => captured = e, () => { });

        var first = new InvalidOperationException("first");
        var second = new InvalidOperationException("second");
        a.OnError(first);
        b.OnError(second);

        captured.Should().BeSameAs(first, "first error wins; subsequent errors from other sources must be dropped");
    }

    [Fact]
    public void OnNext_AfterError_IsIgnored()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        var received = new List<string>();
        Exception? captured = null;
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(received.Add, e => captured = e);

        a.OnNext(1);
        b.OnNext("x");
        received.Should().Equal("1:x");

        var error = new InvalidOperationException();
        a.OnError(error);

        a.OnNext(2);
        b.OnNext("y");
        received.Should().Equal(new[] { "1:x" }, "no further OnNext must arrive after OnError has fired");
        captured.Should().BeSameAs(error);
    }

    [Fact]
    public void OnCompleted_AfterError_IsIgnored()
    {
        using var a = new Subject<int>();
        using var b = new Subject<string>();

        Exception? captured = null;
        var completed = false;
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => $"{x}:{y}").Subscribe(_ => { }, e => captured = e, () => completed = true);

        var error = new InvalidOperationException();
        a.OnError(error);
        b.OnCompleted();

        captured.Should().BeSameAs(error);
        completed.Should().BeFalse("a late OnCompleted from a surviving source must not arrive after OnError");
    }

    [Fact]
    public void ResultSelector_ReceivesMostRecentValueFromEachSource()
    {
        using var a = new Subject<int>();
        using var b = new Subject<int>();

        var received = new List<int>();
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => x * 10 + y).Subscribe(received.Add);

        a.OnNext(1);
        b.OnNext(2);
        received.Should().Equal(12);

        a.OnNext(3);
        received.Should().Equal(new[] { 12, 32 }, "the second source's most recent value (2) must still be in effect");

        b.OnNext(4);
        received.Should().Equal(12, 32, 34);
    }

    [Fact]
    public void SynchronousValues_AtSubscribeTime_AreCombinedCorrectly()
    {
        // Behaviour subjects deliver their initial value synchronously at Subscribe time.
        // The helper must capture the first source's value before subscribing to the second,
        // and immediately emit when the second source's initial value arrives.
        using var a = new System.Reactive.Subjects.BehaviorSubject<int>(7);
        using var b = new System.Reactive.Subjects.BehaviorSubject<int>(11);

        var received = new List<int>();
        using var sub = a.UnsynchronizedCombineLatest(b, (x, y) => x + y).Subscribe(received.Add);

        received.Should().Equal(new[] { 18 }, "both subjects delivered synchronously at subscribe time");
    }

    [Fact]
    public void SynchronousCompletion_BeforeOther_StillCompletesOnlyAfterBoth()
    {
        var immediate = System.Reactive.Linq.Observable.Empty<int>();
        using var live = new Subject<int>();

        var completed = false;
        using var sub = immediate.UnsynchronizedCombineLatest(live, (x, y) => x + y).Subscribe(_ => { }, () => completed = true);

        completed.Should().BeFalse("the live source has not completed yet");

        live.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void SynchronousError_BeforeOther_TerminatesImmediately()
    {
        var error = new InvalidOperationException();
        var immediate = System.Reactive.Linq.Observable.Throw<int>(error);
        using var live = new Subject<int>();

        Exception? captured = null;
        using var sub = immediate.UnsynchronizedCombineLatest(live, (x, y) => x + y).Subscribe(_ => { }, e => captured = e);

        captured.Should().BeSameAs(error);
    }
}
