using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Every documented way of calling <c>BatchIf</c> has to pick an overload on its own. Defaults on
/// the parameters that tell the overloads apart made two of these shapes ambiguous, so they did not
/// compile at all. These assertions are almost incidental: the value is that the file builds.
/// </summary>
public class BatchIfOverloadFixture
{
    [Fact]
    public void EveryOverloadShapeResolves()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var pause = new Subject<bool>();
        using var timer = new Subject<Unit>();

        using var pauseOnly = source.Connect().BatchIf(pause).Subscribe();
        using var withScheduler = source.Connect().BatchIf(pause, Scheduler.Immediate).Subscribe();
        using var withInitialState = source.Connect().BatchIf(pause, true).Subscribe();
        using var withInitialStateAndScheduler = source.Connect().BatchIf(pause, true, Scheduler.Immediate).Subscribe();
        using var withTimeOut = source.Connect().BatchIf(pause, TimeSpan.FromSeconds(1)).Subscribe();
        using var withTimeOutAndScheduler = source.Connect().BatchIf(pause, TimeSpan.FromSeconds(1), Scheduler.Immediate).Subscribe();
        using var withInitialStateAndTimeOut = source.Connect().BatchIf(pause, true, TimeSpan.FromSeconds(1)).Subscribe();
        using var withTimer = source.Connect().BatchIf(pause, true, timer).Subscribe();

        source.Invoking(s => s.AddOrUpdate(new Person("Name", 1))).Should().NotThrow();
    }

    [Fact]
    public void PauseSelectorOnlyStartsUnpaused()
    {
        // The shortest form has to keep delegating with initialPauseState false, rather than
        // silently binding to an overload that starts paused.
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var pause = new Subject<bool>();

        using var results = source.Connect().BatchIf(pause).AsAggregator();

        source.AddOrUpdate(new Person("Name", 1));

        results.Data.Count.Should().Be(1, "nothing has asked for buffering yet");
    }
}
