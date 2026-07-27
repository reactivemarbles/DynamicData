using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Terminal event behaviour for the combining operators.
/// </summary>
public class CombinerCompletionFixture
{
    [Fact]
    public void CombinersCompleteWhenEverySourceCompletes()
    {
        foreach (var combine in new Func<IObservable<IChangeSet<Person, string>>, IObservable<IChangeSet<Person, string>>, IObservable<IChangeSet<Person, string>>>[]
        {
            static (a, b) => ObservableCacheEx.And(a, b),
            static (a, b) => a.Or(b),
            static (a, b) => a.Except(b),
            static (a, b) => a.Xor(b),
        })
        {
            using var first = new Subject<IChangeSet<Person, string>>();
            using var second = new Subject<IChangeSet<Person, string>>();
            var completed = false;

            using var subscription = combine(first, second).Subscribe(_ => { }, () => completed = true);

            first.OnCompleted();
            completed.Should().BeFalse("the second source is still live");

            second.OnCompleted();
            completed.Should().BeTrue("every source has now finished");
        }
    }

    [Fact]
    public void CombinersDeliverErrorFromAnySource()
    {
        using var first = new Subject<IChangeSet<Person, string>>();
        using var second = new Subject<IChangeSet<Person, string>>();
        Exception? error = null;

        using var subscription = first.Or(second).Subscribe(_ => { }, ex => error = ex, () => { });

        second.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void DynamicCombinerCompletesWhenEverySourceCompletes()
    {
        using var sources = new SourceList<IObservable<IChangeSet<Person, string>>>();
        using var first = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        sources.Add(first);

        using var subscription = sources.Or().Subscribe(_ => { }, () => completed = true);

        first.OnCompleted();
        sources.Dispose();

        completed.Should().BeTrue();
    }
}
