namespace DynamicData.Tests.List;

public class SourceListPreviewFixture : IDisposable
{
    private readonly ISourceList<int> _source;

    public SourceListPreviewFixture() => _source = new SourceList<int>();

    [Test]
    public async Task ChangesAreNotYetAppliedDuringPreview()
    {
        _source.Clear();
        var previewWasInvoked = false;
        var countDuringPreview = -1;
        var hadItemsDuringPreview = true;

        // On preview, make sure the list is empty
        var d = _source.Preview().Subscribe(
            _ =>
            {
                previewWasInvoked = true;
                countDuringPreview = _source.Count;
                hadItemsDuringPreview = _source.Items.Any();
            });

        // Trigger a change
        _source.Add(1);

        // Cleanup
        d.Dispose();

        await Assert.That(previewWasInvoked).IsTrue();
        await Assert.That(countDuringPreview).IsEqualTo(0);
        await Assert.That(hadItemsDuringPreview).IsFalse();
    }

    [Test]
    public async Task ConnectPreviewPredicateIsApplied()
    {
        _source.Clear();

        // Collect preview messages about even numbers only
        var aggregator = _source.Preview(i => i % 2 == 0).AsAggregator();

        // Trigger changes
        _source.Add(1);
        _source.Add(2);

        await Assert.That(aggregator.Messages.Count == 1).IsTrue();
        await Assert.That(aggregator.Messages[0].Count == 1).IsTrue();
        await Assert.That(aggregator.Messages[0].First().Item.Current == 2).IsTrue();
        await Assert.That(aggregator.Messages[0].First().Reason == ListChangeReason.Add).IsTrue();

        // Cleanup
        aggregator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task FormNewListFromChanges()
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

        await Assert.That(aggregator.Messages.Count == 1).IsTrue();
        await Assert.That(aggregator.Messages[0].Count == 1).IsTrue();
        await Assert.That(aggregator.Messages[0].First().Item.Current == 2).IsTrue();
        await Assert.That(aggregator.Messages[0].First().Reason == ListChangeReason.Add).IsTrue();

        // Cleanup
        aggregator.Dispose();
    }

    [Test]
    public async Task NoChangesAllowedDuringPreview()
    {
        // On preview, try adding an arbitrary item
        Exception? previewException = null;
        var d = _source.Preview().Subscribe(_ =>
        {
            try
            {
                _source.Add(1);
            }
            catch (Exception error)
            {
                previewException = error;
            }
        });

        // Trigger a change
        _source.Add(1);

        // Cleanup
        d.Dispose();

        await Assert.That(previewException).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task PreviewEventsAreCorrect()
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

        await Assert.That(preview.Messages.SequenceEqual(connect.Messages)).IsTrue();
        await Assert.That(_source.Items.SequenceEqual(new[] { 3, 1, 4, 5 })).IsTrue();
    }

    [Test]
    public async Task RecursiveEditsHavePostponedEvents()
    {
        var preview = _source.Preview().AsAggregator();
        var connect = _source.Connect().AsAggregator();
        var previewMessageCountDuringEdit = -1;
        var connectMessageCountDuringEdit = -1;

        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.Add(1));
                previewMessageCountDuringEdit = preview.Messages.Count;
                connectMessageCountDuringEdit = connect.Messages.Count;
            });

        await Assert.That(previewMessageCountDuringEdit).IsEqualTo(0);
        await Assert.That(connectMessageCountDuringEdit).IsEqualTo(0);
        await Assert.That(preview.Messages.Count).IsEqualTo(1);
        await Assert.That(connect.Messages.Count).IsEqualTo(1);

        await Assert.That(_source.Items.SequenceEqual(new[] { 1 })).IsTrue();
    }

    [Test]
    public async Task RecursiveEditsWork()
    {
        int[] sourceItemsDuringEdit = [];
        int[] editorItemsDuringEdit = [];

        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.Add(1));
                sourceItemsDuringEdit = _source.Items.ToArray();
                editorItemsDuringEdit = l.ToArray();
            });

        await Assert.That(sourceItemsDuringEdit).IsEquivalentTo(new[] { 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(editorItemsDuringEdit).IsEquivalentTo(new[] { 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(_source.Items.SequenceEqual(new[] { 1 })).IsTrue();
    }
}
