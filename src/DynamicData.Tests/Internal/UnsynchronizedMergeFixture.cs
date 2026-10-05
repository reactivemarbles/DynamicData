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
/// Focused behavioural tests for <see cref="SynchronizeSafeExtensions.UnsynchronizedMerge{T}(IObservable{T}, IObservable{T}[])"/>.
/// Covers the contract the helper has to honour as a drop-in <see cref="System.Reactive.Linq.Observable.Merge{TSource}(IObservable{TSource}[])"/>
/// replacement: subscription order, all-must-complete OnCompleted, first-error-wins OnError, and synchronous terminal
/// notifications.
/// </summary>
public sealed class UnsynchronizedMergeFixture
{
    [Fact]
    public void OnNext_FromBothSources_IsForwardedInArrivalOrder()
    {
        using var a = new Subject<int>();
        using var b = new Subject<int>();

        var received = new List<int>();
        using var sub = a.UnsynchronizedMerge(b).Subscribe(received.Add);

        a.OnNext(1);
        b.OnNext(2);
        a.OnNext(3);
        b.OnNext(4);

        received.Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void OnCompleted_FiresOnlyAfterAllSourcesComplete()
    {
        using var a = new Subject<int>();
        using var b = new Subject<int>();
        using var c = new Subject<int>();

        var completed = false;
        using var sub = a.UnsynchronizedMerge(b, c).Subscribe(_ => { }, () => completed = true);

        a.OnCompleted();
        completed.Should().BeFalse("a single source completion must not terminate the merged stream");

        b.OnCompleted();
        completed.Should().BeFalse("two of three completions still leave one source live");

        c.OnCompleted();
        completed.Should().BeTrue("after every source has completed the merged stream must emit OnCompleted");
    }

    [Fact]
    public void OnError_FromAnySource_TerminatesImmediately()
    {
        using var a = new Subject<int>();
        using var b = new Subject<int>();
        using var c = new Subject<int>();

        Exception? captured = null;
        var completed = false;
        using var sub = a.UnsynchronizedMerge(b, c).Subscribe(_ => { }, e => captured = e, () => completed = true);

        var error = new InvalidOperationException("first");
        b.OnError(error);

        captured.Should().BeSameAs(error);
        completed.Should().BeFalse("OnCompleted must not fire after OnError");
    }

    [Fact]
    public void OnError_AfterFirstError_IsIgnored()
    {
        using var a = new Subject<int>();
        using var b = new Subject<int>();

        Exception? captured = null;
        using var sub = a.UnsynchronizedMerge(b).Subscribe(_ => { }, e => captured = e, () => { });

        var first = new InvalidOperationException("first");
        var second = new InvalidOperationException("second");
        a.OnError(first);
        b.OnError(second);

        captured.Should().BeSameAs(first, "first error wins; subsequent errors from other sources must be dropped");
    }

    [Fact]
    public void OnCompleted_AfterError_IsIgnored()
    {
        using var a = new Subject<int>();
        using var b = new Subject<int>();

        Exception? captured = null;
        var completed = false;
        using var sub = a.UnsynchronizedMerge(b).Subscribe(_ => { }, e => captured = e, () => completed = true);

        var error = new InvalidOperationException();
        a.OnError(error);
        b.OnCompleted();

        captured.Should().BeSameAs(error);
        completed.Should().BeFalse("a late OnCompleted from a surviving source must not arrive after OnError has fired");
    }

    [Fact]
    public void Subscription_OccursInArgumentOrder()
    {
        var subscribed = new List<int>();
        var first = System.Reactive.Linq.Observable.Create<int>(o => { subscribed.Add(0); return () => { }; });
        var second = System.Reactive.Linq.Observable.Create<int>(o => { subscribed.Add(1); return () => { }; });
        var third = System.Reactive.Linq.Observable.Create<int>(o => { subscribed.Add(2); return () => { }; });

        using var sub = first.UnsynchronizedMerge(second, third).Subscribe(_ => { });

        subscribed.Should().Equal(0, 1, 2);
    }

    [Fact]
    public void SynchronousTerminal_BeforeOtherSourcesSubscribe_IsHandled()
    {
        // A source that completes synchronously at subscribe time decrements the pending counter immediately.
        // If the helper miscounted, the merged stream would either complete prematurely or never complete.
        var immediate = System.Reactive.Linq.Observable.Empty<int>();
        using var live = new Subject<int>();

        var completed = false;
        using var sub = immediate.UnsynchronizedMerge(live).Subscribe(_ => { }, () => completed = true);

        completed.Should().BeFalse("the live source has not completed yet");

        live.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void SynchronousError_BeforeOtherSourcesSubscribe_TerminatesImmediately()
    {
        var error = new InvalidOperationException();
        var immediate = System.Reactive.Linq.Observable.Throw<int>(error);
        using var live = new Subject<int>();

        Exception? captured = null;
        using var sub = immediate.UnsynchronizedMerge(live).Subscribe(_ => { }, e => captured = e);

        captured.Should().BeSameAs(error);
    }

    [Fact]
    public void NoOthers_FallsBackToFirstAlone()
    {
        // Boundary: zero entries in the params array. Behaviour must mirror Observable.Merge over a single source.
        using var a = new Subject<int>();
        var received = new List<int>();
        var completed = false;
        using var sub = a.UnsynchronizedMerge().Subscribe(received.Add, () => completed = true);

        a.OnNext(7);
        a.OnNext(11);
        a.OnCompleted();

        received.Should().Equal(7, 11);
        completed.Should().BeTrue();
    }
}