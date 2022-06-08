using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.Cache;

public class ToObservableChangeSetFixture : ReactiveTest, IDisposable
{
    private readonly ITestOutputHelper _outputHelper;

    private readonly IDisposable _disposable;

    private readonly IObservable<Person> _observable;

    private readonly Person _person1 = new("One", 1);

    private readonly Person _person2 = new("Two", 2);

    private readonly Person _person3 = new("Three", 3);

    private readonly TestScheduler _scheduler;

    private readonly List<Person> _target;

    public ToObservableChangeSetFixture(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _scheduler = new TestScheduler();
        _observable = _scheduler.CreateColdObservable(OnNext(1, _person1), OnNext(2, _person2), OnNext(3, _person3));

        _target = new List<Person>();

        _disposable = _observable.ToObservableChangeSet(p => p.Key, limitSizeTo: 2, scheduler: _scheduler).Clone(_target).Subscribe();
    }

    [Fact]
    public void DictionaryOrder()
    {
        var xxx = new Dictionary<int, int>();


        foreach (var i in Enumerable.Range(1,10))
        {
            xxx[i] = i;
        }

        xxx[5] = 999;
    }



    [Fact]
    public void ShouldLimitSizeOfBoundCollection()
    {
        _scheduler.AdvanceTo(2);
        _target.Count.Should().Be(2, "Should be 2 item in target collection");

        _scheduler.AdvanceTo(3);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks); //push time forward as size limit is checked for after the event 

        _target.Count.Should().Be(2, "Should be 2 item in target collection because of size limit");
    }


    public void Dispose() => _disposable.Dispose();
}
