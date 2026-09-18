namespace DynamicData.Tests.List;

public sealed class DisposeManyFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.Signal<IChangeSet<DisposableObject>> _changeSetsSource;

    private readonly SourceList<DisposableObject> _itemsSource;

    private readonly ChangeSetAggregator<DisposableObject> _results;

    private readonly List<DisposableObject> _itemsDisposedBeforeDownstreamNotification = [];

    public DisposeManyFixture()
    {
        _changeSetsSource = new();
        _itemsSource = new();
        _results = new(Observable.Merge(_changeSetsSource, _itemsSource.Connect())
            .DisposeMany()
            .Do(onNext: changeSet =>
                {
                    foreach (var change in changeSet)
                    {
                        if (change.Item.Current is not null)
                            TrackIfDisposed(change.Item.Current);

                        if (change.Item.Previous.HasValue)
                            TrackIfDisposed(change.Item.Previous.Value);

                        if (change.Range is not null)
                            foreach (var item in change.Range)
                                TrackIfDisposed(item);
                    }
                },
                onError: _ =>
                {
                    foreach (var item in _itemsSource.Items)
                        TrackIfDisposed(item);
                },
                onCompleted: () =>
                {
                    foreach (var item in _itemsSource.Items)
                        TrackIfDisposed(item);
                }));
    }

    private void TrackIfDisposed(DisposableObject item)
    {
        if (item.IsDisposed)
        {
            _itemsDisposedBeforeDownstreamNotification.Add(item);
        }
    }

    public void Dispose()
    {
        _changeSetsSource.Dispose();
        _itemsSource.Dispose();
        _results.Dispose();
    }

    [Test]
    // Verifies https://github.com/reactivemarbles/DynamicData/issues/668
    public async Task ErrorsArePropagated()
    {
        var error = new Exception("Test Exception");

        var source = Observable.Throw<IChangeSet<object>>(error)
            .DisposeMany();

        var exception = await Assert.That(() => source.Subscribe()).Throws<Exception>();
        await Assert.That(exception).IsSameReferenceAs(error);

        var receivedError = null as Exception;
        source.Subscribe(
            onNext: static _ => { },
            onError: error => receivedError = error);
        await Assert.That(receivedError).IsSameReferenceAs(error);
    }

    [Test]
    public async Task ItemsAreDisposedAfterRemovalOrReplacement()
    {
        var items = Enumerable.Range(1, 10)
            .Select(id => new DisposableObject(id))
            .ToArray();

        // Exercise a variety of types of changesets.
        _itemsSource.Add(items[0]); // Trivial single add
        _itemsSource.AddRange(items[1..3]); // Trivial range add
        _itemsSource.Insert(index: 1, item: items[3]); // Non-trivial single add
        _itemsSource.InsertRange(index: 2, items: items[4..6]); // Non-trivial range add
        _itemsSource.RemoveAt(index: 3); // Single remove
        _itemsSource.RemoveRange(index: 2, count: 2); // Range remove
        _itemsSource.ReplaceAt(index: 1, item: items[6]); // Replace
        _itemsSource.Move(1, 0); // Move
        _itemsSource.Clear(); // Clear
        _itemsSource.AddRange(items[7..10]);
        _changeSetsSource.OnNext(new ChangeSet<DisposableObject>() // Refresh
        {
            new(ListChangeReason.Refresh, current: _itemsSource.Items[0], index: 0)
        });

        await Assert.That(_results.Exception).IsNull();
        await Assert.That(_itemsDisposedBeforeDownstreamNotification).IsEmpty().Because("items should not be disposed until after downstream notifications are processed");
        await Assert.That(_results.Messages.Count).IsEqualTo(11).Because("11 updates were made to the source");
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("3 items were not removed from the list");
        await Assert.That(_results.Data.Items.All(item => item.IsDisposed)).IsFalse().Because("items remaining in the list should not be disposed");
        await Assert.That(items.Except(_results.Data.Items).All(item => item.IsDisposed)).IsTrue().Because("items removed from the list should be disposed");
    }

    [Test]
    public async Task RemainingItemsAreDisposedAfterCompleted()
    {
        _itemsSource.AddRange(new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3),
        });
        _itemsSource.Dispose();
        _changeSetsSource.OnCompleted();

        await Assert.That(_results.Exception).IsNull();
        await Assert.That(_itemsDisposedBeforeDownstreamNotification).IsEmpty().Because("items should not be disposed until after downstream notifications are processed");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("1 update was made to the list");
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("3 items were not removed from the list");
        await Assert.That(_results.Data.Items.All(item => item.IsDisposed)).IsTrue().Because("items remaining in the list should be disposed");
    }

    [Test]
    public async Task RemainingItemsAreDisposedAfterError()
    {
        _itemsSource.Add(new(1));

        var error = new Exception("Test Exception");
        _changeSetsSource.OnError(error);

        _itemsSource.Add(new(2));

        await Assert.That(_results.Exception).IsEqualTo(error);
        await Assert.That(_itemsDisposedBeforeDownstreamNotification).IsEmpty().Because("items should not be disposed until after downstream notifications are processed");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("1 update was made to the list");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("1 item was not removed from the list");
        await Assert.That(_results.Data.Items.All(item => item.IsDisposed)).IsTrue().Because("Items remaining in the list should be disposed");
    }

    [Test]
    public async Task RemainingItemsAreDisposedAfterUnsubscription()
    {
        var items = new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3)
        };

        _itemsSource.AddRange(items);

        _results.Dispose();

        await Assert.That(items.All(item => item.IsDisposed)).IsTrue().Because("Items remaining in the list should be disposed");
    }

    private class DisposableObject(int id) : IDisposable
    {
        public int Id { get; private set; } = id;

        public bool IsDisposed { get; private set; }

        public void Dispose()
            => IsDisposed = true;
    }
}
