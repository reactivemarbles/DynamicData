// Copyright (c) 2011-2026 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Cache;

public partial class BatchIfFixture
{
    private const int OverloadSeed = 0x2409_1153;

    private readonly Randomizer _overloadRandomizer = new(OverloadSeed);

    /// <summary>
    /// Verifies that every supported call shape compiles and binds unambiguously, covering both the shapes that
    /// compiled before the named timer overload was added and the two shapes that overload restores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Compilation is the primary assertion; a regression surfaces as a compiler error rather than a failing assertion.
    /// The shapes fall into two groups with different baselines, and the distinction matters when reading this as
    /// evidence.
    /// </para>
    /// <para>
    /// The preserved shapes all compiled against the pre-change base commit and must keep doing so; a regression there
    /// is a source break and surfaces as <c>CS0121</c>. Two of them constrain the overload set.
    /// <c>BatchIf(pause, null, scheduler)</c> must keep binding to the <see cref="TimeSpan"/> timeout overload, which
    /// rules out any added overload whose fourth positional parameter accepts an
    /// <see cref="System.Reactive.Concurrency.IScheduler"/>. A fully named call that supplies <c>initialPauseState</c>
    /// alongside <c>timer</c> must keep binding to the overload that declares both, which rules out an added overload
    /// that declares an <c>initialPauseState</c> parameter of its own.
    /// </para>
    /// <para>
    /// The restored shapes did not compile against the base commit and are the point of this change. There the only
    /// overload accepting a timer required <c>initialPauseState</c>, so omitting it failed with <c>CS7036</c>.
    /// </para>
    /// <para>
    /// This guards overload resolution rather than behavior. The behavior reached through each shape is already covered
    /// by the tests for the overloads that actually implement it.
    /// </para>
    /// </remarks>
    [Fact]
    public void SupportedCallShapes_RemainUnambiguous()
    {
        // Arrange
        using var pause = new Subject<bool>();
        using var timer = new Subject<Unit>();
        var changes = _source.Connect();
        var timeOut = TimeSpan.FromMilliseconds(_overloadRandomizer.Int(10, 500));

        // Act
        var preservedShapes = new[]
        {
            changes.BatchIf(pause),
            changes.BatchIf(pause, _scheduler),
            changes.BatchIf(pause, true),
            changes.BatchIf(pause, true, _scheduler),
            changes.BatchIf(pause, null, _scheduler),
            changes.BatchIf(pause, timeOut),
            changes.BatchIf(pause, timeOut, _scheduler),
            changes.BatchIf(pause, true, timeOut),
            changes.BatchIf(pause, true, timeOut, _scheduler),
            changes.BatchIf(pause, true, timer),
            changes.BatchIf(pause, true, timer, _scheduler),
            changes.BatchIf(pause, initialPauseState: true, timer: timer, scheduler: _scheduler),
            changes.BatchIf(pause, timer: timer, initialPauseState: true, scheduler: _scheduler),
        };

        var restoredShapes = new[]
        {
            changes.BatchIf(pause, timer: timer),
            changes.BatchIf(pause, timer: timer, scheduler: _scheduler),
        };

        // Assert
        preservedShapes.Should().OnlyContain(observable => observable != null, because: "every shape that compiled before this change must still bind");
        restoredShapes.Should().OnlyContain(observable => observable != null, because: "the named timer shapes from issue 1175 must bind");
    }
}
