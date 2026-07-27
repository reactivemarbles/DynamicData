using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

/// <summary>
/// Terminal event behaviour for the combining operators.
/// </summary>
public class CombinerCompletionFixture
{
    [Fact]
    public void CombinersCompleteWhenEverySourceCompletes()
    {
        foreach (var combine in new Func<IObservable<IChangeSet<Person>>, IObservable<IChangeSet<Person>>, IObservable<IChangeSet<Person>>>[]
        {
            static (a, b) => ObservableListEx.And(a, b),
            static (a, b) => a.Or(b),
            static (a, b) => a.Except(b),
            static (a, b) => a.Xor(b),
        })
        {
            using var first = new Subject<IChangeSet<Person>>();
            using var second = new Subject<IChangeSet<Person>>();
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
        using var first = new Subject<IChangeSet<Person>>();
        using var second = new Subject<IChangeSet<Person>>();
        Exception? error = null;

        using var subscription = first.Or(second).Subscribe(_ => { }, ex => error = ex, () => { });

        second.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

}
