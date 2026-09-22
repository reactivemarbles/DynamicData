// Copyright (c) 2011-2026 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

using Randomizer = Bogus.Randomizer;

namespace DynamicData.Tests.Cache;

public partial class BatchIfFixture
{
    private const int OverloadSeed = 0x2409_1153;

    private readonly Randomizer _overloadRandomizer = new(OverloadSeed);

    /// <summary>Verifies that named timer overloads start without buffering unless a pause is requested.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedTimerWithoutInitialState_StartsUnpaused(bool specifyScheduler)
    {
        // Arrange
        using var pause = new Subject<bool>();
        using var timer = new Subject<Unit>();
        var changes = _source.Connect();
        var batched = specifyScheduler
            ? changes.BatchIf(pause, timer: timer, scheduler: _scheduler)
            : changes.BatchIf(pause, timer: timer);
        using var results = batched.AsAggregator();
        var person = new Person(_overloadRandomizer.String2(_overloadRandomizer.Int(5, 20)), _overloadRandomizer.Int(1, 100));

        // Act
        _source.AddOrUpdate(person);

        // Assert
        results.Error.Should().BeNull(because: "a named timer is a supported overload shape");
        results.Data.Items.Should().Equal([person], because: "omitting the initial pause state means changes initially pass through");
    }

    /// <summary>Verifies that a named timer flushes changes accumulated during an explicit pause.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamedTimerWithoutInitialState_FlushesOnTimer(bool specifyScheduler)
    {
        // Arrange
        using var pause = new Subject<bool>();
        using var timer = new Subject<Unit>();
        var changes = _source.Connect();
        var batched = specifyScheduler
            ? changes.BatchIf(pause, timer: timer, scheduler: _scheduler)
            : changes.BatchIf(pause, timer: timer);
        using var results = batched.AsAggregator();
        var person = new Person(_overloadRandomizer.String2(_overloadRandomizer.Int(5, 20)), _overloadRandomizer.Int(1, 100));
        pause.OnNext(true);
        _source.AddOrUpdate(person);

        // Act
        timer.OnNext(Unit.Default);

        // Assert
        results.Error.Should().BeNull(because: "the timer must flush a valid buffered changeset");
        results.Data.Items.Should().Equal([person], because: "the timer overload must forward the accumulated item");
    }
}
