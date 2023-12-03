using System;
using System.Linq;

using Xunit;

namespace DynamicData.Tests.List;

public class SourceListPreviewFixture : IDisposable
{
    private readonly ISourceList<int> _source;

    public SourceListPreviewFixture() => _source = new SourceList<int>();

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
        _source.Add(1);

        // Cleanup
        d.Dispose();
    }

    [Fact]
    public void ConnectPreviewPredicateIsApplied()
    {
        _source.Clear();

        // Collect preview messages about even numbers only
        var aggregator = _source.Preview(i => i % 2 == 0).AsAggregator();

        // Trigger changes
        _source.Add(1);
        _source.Add(2);

        Assert.True(aggregator.Messages.Count == 1);
        Assert.True(aggregator.Messages[0].Count == 1);
        Assert.True(aggregator.Messages[0].First().Item.Current == 2);
        Assert.True(aggregator.Messages[0].First().Reason == ListChangeReason.Add);

        // Cleanup
        aggregator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void FormNewListFromChanges()
    {
        _source.Clear();

        _source.AddRange(Enumerable.Range(1, 100));

        // Collect preview messages about even numbers only
        var aggregator = _source.Preview(i => i % 2 == 0).AsAggregator();

        _source.RemoveAt(10);
        _source.RemoveRange(10, 5);
        // Trigger changes
        _source.Add(1);
        _source.Add(2);

        Assert.True(aggregator.Messages.Count == 1);
        Assert.True(aggregator.Messages[0].Count == 1);
        Assert.True(aggregator.Messages[0].First().Item.Current == 2);
        Assert.True(aggregator.Messages[0].First().Reason == ListChangeReason.Add);

        // Cleanup
        aggregator.Dispose();
    }

    [Fact]
    public void NoChangesAllowedDuringPreview()
    {
        // On preview, try adding an arbitrary item
        var d = _source.Preview().Subscribe(_ => Assert.Throws<InvalidOperationException>(() => _source.Add(1)));

        // Trigger a change
        _source.Add(1);

        // Cleanup
        d.Dispose();
    }

    [Fact]
    public void PreviewEventsAreCorrect()
    {
        var preview = _source.Preview().AsAggregator();
        var connect = _source.Connect().AsAggregator();
        _source.Edit(
            l =>
            {
                l.Add(1);
                _source.Edit(l2 => l2.Add(2));
                l.Remove(2);
                l.AddRange(new[] { 3, 4, 5 });
                l.Move(1, 0);
            });

        Assert.True(preview.Messages.SequenceEqual(connect.Messages));
        Assert.True(_source.Items.SequenceEqual(new[] { 3, 1, 4, 5 }));
    }

    [Fact]
    public void RecursiveEditsHavePostponedEvents()
    {
        var preview = _source.Preview().AsAggregator();
        var connect = _source.Connect().AsAggregator();
        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.Add(1));
                Assert.Equal(0, preview.Messages.Count);
                Assert.Equal(0, connect.Messages.Count);
            });

        Assert.Equal(1, preview.Messages.Count);
        Assert.Equal(1, connect.Messages.Count);

        Assert.True(_source.Items.SequenceEqual(new[] { 1 }));
    }

    [Fact]
    public void RecursiveEditsWork()
    {
        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.Add(1));
                Assert.True(_source.Items.SequenceEqual(new[] { 1 }));
                Assert.True(l.SequenceEqual(new[] { 1 }));
            });

        Assert.True(_source.Items.SequenceEqual(new[] { 1 }));
    }
}
