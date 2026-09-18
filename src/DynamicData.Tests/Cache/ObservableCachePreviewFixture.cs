using DynamicData.Tests.Domain;

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

    [Test]
    public async Task ChangesAreNotYetAppliedDuringPreview()
    {
        _source.Clear();

        // On preview, make sure the list is empty
        var countDuringPreview = -1;
        var anyItemsDuringPreview = true;
        var d = _source.Preview().Subscribe(
            _ =>
            {
                countDuringPreview = _source.Count;
                anyItemsDuringPreview = _source.Items.Any();
            });

        // Trigger a change
        _source.AddOrUpdate(new Person("A", 1));

        await Assert.That(countDuringPreview).IsEqualTo(0);
        await Assert.That(anyItemsDuringPreview).IsFalse();

        // Cleanup
        d.Dispose();
    }

    [Test]
    public async Task ConnectPreviewPredicateIsApplied()
    {
        _source.Clear();

        // Collect preview messages about even numbers only
        var aggregator = _source.Preview(i => i.Age == 2).AsAggregator();

        // Trigger changes
        _source.AddOrUpdate(new Person("A", 1));
        _source.AddOrUpdate(new Person("B", 2));
        _source.AddOrUpdate(new Person("C", 3));

        await Assert.That(aggregator.Messages.Count == 1).IsTrue();
        await Assert.That(aggregator.Messages[0].Count == 1).IsTrue();
        await Assert.That(aggregator.Messages[0].First().Key == "B").IsTrue();
        await Assert.That(aggregator.Messages[0].First().Reason == ChangeReason.Add).IsTrue();

        // Cleanup
        aggregator.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task NoChangesAllowedDuringPreview()
    {
        // On preview, try adding an arbitrary item
        Exception? receivedException = null;
        var d = _source.Preview().Subscribe(_ =>
        {
            try
            {
                _source.AddOrUpdate(new Person("A", 1));
            }
            catch (Exception exception)
            {
                receivedException = exception;
            }
        });

        // Trigger a change
        _source.AddOrUpdate(new Person("B", 2));

        await Assert.That(receivedException).IsTypeOf<InvalidOperationException>();

        // Cleanup
        d.Dispose();
    }

    [Test]
    public async Task PreviewEventsAreCorrect()
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

        await Assert.That(preview.Messages.SequenceEqual(connect.Messages)).IsTrue();
        await Assert.That(_source.KeyValues.OrderBy(t => t.Value.Age).Select(t => t.Value.Age).SequenceEqual(new[] { 2, 3 })).IsTrue();
    }

    [Test]
    public async Task RecursiveEditsHavePostponedEvents()
    {
        var person = new Person("A", 1);

        var preview = _source.Preview().AsAggregator();
        var connect = _source.Connect().AsAggregator();
        var previewCountDuringEdit = -1;
        var connectCountDuringEdit = -1;
        _source.Edit(
            l =>
            {
                _source.Edit(l2 => l2.AddOrUpdate(person));
                previewCountDuringEdit = preview.Messages.Count;
                connectCountDuringEdit = connect.Messages.Count;
            });

        await Assert.That(previewCountDuringEdit).IsEqualTo(0);
        await Assert.That(connectCountDuringEdit).IsEqualTo(0);
        await Assert.That(preview.Messages.Count).IsEqualTo(1);
        await Assert.That(connect.Messages.Count).IsEqualTo(1);

        await Assert.That(_source.Items.SequenceEqual(new[] { person })).IsTrue();
    }

    [Test]
    public async Task RecursiveEditsWork()
    {
        var person = new Person("A", 1);

        Person[] sourceItemsDuringEdit = [];
        Person[] updaterItemsDuringEdit = [];
        _source.Edit(
            l =>
            {
                _source.AddOrUpdate(person);
                sourceItemsDuringEdit = _source.Items.ToArray();
                updaterItemsDuringEdit = l.Items.ToArray();
            });

        await Assert.That(sourceItemsDuringEdit.SequenceEqual(new[] { person })).IsTrue();
        await Assert.That(updaterItemsDuringEdit.SequenceEqual(new[] { person })).IsTrue();
        await Assert.That(_source.Items.SequenceEqual(new[] { person })).IsTrue();
    }
}
