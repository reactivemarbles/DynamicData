using System;
using System.Linq;

using DynamicData.Tests.Domain;

using Xunit;

namespace DynamicData.Tests.Cache;

public class ObservableCachePreviewFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public ObservableCachePreviewFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = _source.Connect().AsAggregator();
    }

    [Fact]
    public void ChangesAreNotYetAppliedDuringPreview()
    {
        _source.Clear();

        // On preview, make sure the list is empty
        var d = _source.Preview().Subscribe(
            _ =>
            {
                Assert.True(_source.Count == 0);
                Assert.True(_source.Items.Any() == false);
            });

        // Trigger a change
        _source.AddOrUpdate(new Person("A", 1));

        // Cleanup
        d.Dispose();
    }

    [Fact]
    public void ConnectPreviewPredicateIsApplied()
    {
        _source.Clear();

        // Collect preview messages about even numbers only
        var aggregator = _source.Preview(i => i.Age == 2).AsAggregator();

        // Trigger changes
        _source.AddOrUpdate(new Person("A", 1));
        _source.AddOrUpdate(new Person("B", 2));
        _source.AddOrUpdate(new Person("C", 3));

        Assert.True(aggregator.Messages.Count == 1);
        Assert.True(aggregator.Messages[0].Count == 1);
        Assert.True(aggregator.Messages[0].First().Key == "B");
        Assert.True(aggregator.Messages[0].First().Reason == ChangeReason.Add);

        // Cleanup
        aggregator.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void NoChangesAllowedDuringPreview()
    {
        // On preview, try adding an arbitrary item
        var d = _source.Preview().Subscribe(_ => Assert.Throws<InvalidOperationException>(() => _source.AddOrUpdate(new Person("A", 1))));

        // Trigger a change
        _source.AddOrUpdate(new Person("B", 2));

        // Cleanup
        d.Dispose();
    }

    [Fact]
    public void PreviewEventsAreCorrect()
    {
        var person = new Person("A", 1);

        var preview = _source.Preview().AsAggregator();
        var connect = _source.Connect().AsAggregator();
        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.AddOrUpdate(person));
                l.Remove(person);
                l.AddOrUpdate(new[] { new Person("B", 2), new Person("C", 3) });
            });

        Assert.True(preview.Messages.SequenceEqual(connect.Messages));
        Assert.True(_source.KeyValues.OrderBy(t => t.Value.Age).Select(t => t.Value.Age).SequenceEqual(new[] { 2, 3 }));
    }

    [Fact]
    public void RecursiveEditsHavePostponedEvents()
    {
        var person = new Person("A", 1);

        var preview = _source.Preview().AsAggregator();
        var connect = _source.Connect().AsAggregator();
        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.AddOrUpdate(person));
                Assert.Equal(0, preview.Messages.Count);
                Assert.Equal(0, connect.Messages.Count);
            });

        Assert.Equal(1, preview.Messages.Count);
        Assert.Equal(1, connect.Messages.Count);

        Assert.True(_source.Items.SequenceEqual(new[] { person }));
    }

    [Fact]
    public void RecursiveEditsWork()
    {
        var person = new Person("A", 1);

        _source.Edit(
            l =>
            {
                _source.AddOrUpdate(person);
                Assert.True(_source.Items.SequenceEqual(new[] { person }));
                Assert.True(l.Items.SequenceEqual(new[] { person }));
            });

        Assert.True(_source.Items.SequenceEqual(new[] { person }));
    }
}
