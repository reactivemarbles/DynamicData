using System;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class SourceCacheFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public SourceCacheFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Key);
        _results = _source.Connect().AsAggregator();
    }

    [Fact]
    public void CanHandleABatchOfUpdates()
    {
        _source.Edit(
            updater =>
            {
                var torequery = new Person("Adult1", 44);

                updater.AddOrUpdate(new Person("Adult1", 40));
                updater.AddOrUpdate(new Person("Adult1", 41));
                updater.AddOrUpdate(new Person("Adult1", 42));
                updater.AddOrUpdate(new Person("Adult1", 43));
                updater.Refresh(torequery);
                updater.Remove(torequery);
                updater.Refresh(torequery);
            });

        _results.Summary.Overall.Count.Should().Be(6, "Should be  6 up`dates");
        _results.Messages.Count.Should().Be(1, "Should be 1 message");
        _results.Messages[0].Adds.Should().Be(1, "Should be 1 update");
        _results.Messages[0].Updates.Should().Be(3, "Should be 3 updates");
        _results.Messages[0].Removes.Should().Be(1, "Should be  1 remove");
        _results.Messages[0].Refreshes.Should().Be(1, "Should be 1 evaluate");

        _results.Data.Count.Should().Be(0, "Should be 1 item in` the cache");
    }

    [Fact]
    public void CountChanged()
    {
        var count = 0;
        var invoked = 0;
        using (_source.CountChanged.Subscribe(
                   c =>
                   {
                       count = c;
                       invoked++;
                   }))
        {
            invoked.Should().Be(1);
            count.Should().Be(0);

            _source.AddOrUpdate(new RandomPersonGenerator().Take(100));
            invoked.Should().Be(2);
            count.Should().Be(100);

            _source.Clear();
            invoked.Should().Be(3);
            count.Should().Be(0);
        }
    }

    [Fact]
    public void CountChangedShouldAlwaysInvokeUponeSubscription()
    {
        int? result = null;
        var subscription = _source.CountChanged.Subscribe(count => result = count);

        result.HasValue.Should().BeTrue();

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        result.Value.Should().Be(0, "Count should be zero");

        subscription.Dispose();
    }

    [Fact]
    public void CountChangedShouldReflectContentsOfCacheInvokeUponSubscription()
    {
        var generator = new RandomPersonGenerator();
        int? result = null;
        var subscription = _source.CountChanged.Subscribe(count => result = count);

        _source.AddOrUpdate(generator.Take(100));

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(100, "Count should be 100");
        subscription.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void SubscribesDisposesCorrectly()
    {
        var called = false;
        var errored = false;
        var completed = false;
        var subscription = _source.Connect().Finally(() => completed = true).Subscribe(updates => { called = true; }, ex => errored = true, () => completed = true);
        _source.AddOrUpdate(new Person("Adult1", 40));

        subscription.Dispose();
        _source.Dispose();

        errored.Should().BeFalse();
        called.Should().BeTrue();
        completed.Should().BeTrue();
    }

    [Fact]
    public void EmptyChanges()
    {
        IChangeSet<Person, string>? change = null;

        using var subscription = _source.Connect(suppressEmptyChangeSets: false)
            .Subscribe(c=> change = c);

        change.Should().NotBeNull();
        change!.Count.Should().Be(0);

    }

    [Fact]
    public void EmptyChangesWithFilter()
    {
        IChangeSet<Person, string>? change = null;

        using var subscription = _source.Connect(p=>p.Age == 20, suppressEmptyChangeSets: false)
            .Subscribe(c => change = c);

        change.Should().NotBeNull();
        change!.Count.Should().Be(0);
    }



    [Fact]
    public void StaticFilterRemove()
    {
        var cache = new SourceCache<SomeObject, int>(x => x.Id);
        
        var above5 = cache.Connect(x => x.Value > 5).AsObservableCache();
        var below5 = cache.Connect(x => x.Value <= 5).AsObservableCache();

        cache.AddOrUpdate(Enumerable.Range(1,10).Select(i=> new SomeObject(i,i)));


        above5.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5).Select(i => new SomeObject(i, i)));
        below5.Items.Should().BeEquivalentTo(Enumerable.Range(1, 5).Select(i => new SomeObject(i, i)));

        //should move from above 5 to below 5
        cache.AddOrUpdate(new SomeObject(6,-1));

        above5.Count.Should().Be(4);
        below5.Count.Should().Be(6);


        above5.Items.Should().BeEquivalentTo(Enumerable.Range(7, 4).Select(i => new SomeObject(i, i)));
        below5.Items.Should().BeEquivalentTo(Enumerable.Range(1, 6).Select(i => new SomeObject(i, i == 6 ? -1 : i)));
    }

    public record class SomeObject(int Id, int Value);

}
