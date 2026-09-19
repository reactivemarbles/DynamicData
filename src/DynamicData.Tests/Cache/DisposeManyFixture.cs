namespace DynamicData.Tests.Cache;

public sealed class DisposeManyFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.Signal<IChangeSet<DisposableObject, int>> _changeSetsSource;

    private readonly List<string> _itemsDisposedBeforeDownstreamNotifications = [];

    private readonly SourceCache<DisposableObject, int> _itemsSource;

    private readonly ChangeSetAggregator<DisposableObject, int> _results;

    public DisposeManyFixture()
    {
        _changeSetsSource = new();
        _itemsSource = new(item => item.Id);
        _results = new(Observable.Merge(_changeSetsSource, _itemsSource.Connect())
            .DisposeMany()
            .Do(onNext: changeSet =>
                {
                    foreach (var change in changeSet)
                    {
                        if (change.Current.IsDisposed)
                        {
                            _itemsDisposedBeforeDownstreamNotifications.Add($"Current item {change.Current.Id}");
                        }

                        if (change.Previous.HasValue)
                        {
                            if (change.Previous.Value.IsDisposed)
                            {
                                _itemsDisposedBeforeDownstreamNotifications.Add($"Previous item {change.Previous.Value.Id}");
                            }
                        }
                    }
                },
                onError: _ =>
                {
                    foreach (var item in _itemsSource.Items)
                    {
                        if (item.IsDisposed)
                        {
                            _itemsDisposedBeforeDownstreamNotifications.Add($"Error item {item.Id}");
                        }
                    }
                },
                onCompleted: () =>
                {
                    foreach (var item in _itemsSource.Items)
                    {
                        if (item.IsDisposed)
                        {
                            _itemsDisposedBeforeDownstreamNotifications.Add($"Completed item {item.Id}");
                        }
                    }
                }));
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

        var source = Observable.Throw<IChangeSet<object, object>>(error)
            .DisposeMany();

        var thrown = Assert.Throws<Exception>(() => source.Subscribe());
        await Assert.That(thrown).IsSameReferenceAs(error);

        var receivedError = null as Exception;
        source.Subscribe(
            onNext: static _ => { },
            onError: error => receivedError = error);
        await Assert.That(receivedError).IsSameReferenceAs(error);
    }

    [Test]
    public async Task ItemsAreDisposedAfterRemovalOrReplacement()
    {
        var items = new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3),
            new DisposableObject(4),
            new DisposableObject(5),
            new DisposableObject(1),
            new DisposableObject(6),
            new DisposableObject(7),
            new DisposableObject(8)
        };

        // Exercise a variety of types of changesets.
        _itemsSource.AddOrUpdate(items[0]); // Single add
        _itemsSource.AddOrUpdate(items[1..5]); // Range add
        _itemsSource.AddOrUpdate(items[5]); // Replace
        _itemsSource.AddOrUpdate(items[5]); // Redundant update
        _itemsSource.RemoveKey(4); // Single remove
        _itemsSource.RemoveKeys(new[] { 1, 2 }); // Range remove
        _itemsSource.Clear(); // Clear
        _itemsSource.AddOrUpdate(items[6..9]);
        _changeSetsSource.OnNext(new ChangeSet<DisposableObject, int>() // Refresh
        {
            new Change<DisposableObject, int>(
                reason: ChangeReason.Refresh,
                key: _itemsSource.Items[0].Id,
                current: _itemsSource.Items[0])
        });
        _changeSetsSource.OnNext(new ChangeSet<DisposableObject, int>() // Move
        {
            new Change<DisposableObject, int>(
                key: _itemsSource.Items[0].Id,
                current: _itemsSource.Items[0],
                currentIndex: 1,
                previousIndex: 0)
        });

        await Assert.That(_results.Error).IsNull();
        await Assert.That(_itemsDisposedBeforeDownstreamNotifications).IsEmpty().Because("items should not be disposed until after downstream notifications are processed");
        await Assert.That(_results.Messages.Count).IsEqualTo(10).Because("10 updates were made to the source");
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("3 items were not removed from the list");
        await Assert.That(_results.Data.Items.All(item => item.IsDisposed)).IsFalse().Because("items remaining in the list should not be disposed");
        await Assert.That(items.Except(_results.Data.Items).All(item => item.IsDisposed)).IsTrue().Because("items removed from the list should be disposed");
    }

    [Test]
    public async Task RemainingItemsAreDisposedAfterCompleted()
    {
        _itemsSource.AddOrUpdate(new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3)
        });

        _itemsSource.Dispose();
        _changeSetsSource.OnCompleted();

        await Assert.That(_results.Error).IsNull();
        await Assert.That(_itemsDisposedBeforeDownstreamNotifications).IsEmpty().Because("items should not be disposed until after downstream notifications are processed");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("1 update was made to the source");
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("3 items were not removed from the list");
        await Assert.That(_results.Data.Items.All(item => item.IsDisposed)).IsTrue().Because("Items remaining in the list should be disposed");
    }

    [Test]
    public async Task RemainingItemsAreDisposedAfterError()
    {
        _itemsSource.AddOrUpdate(new DisposableObject(1));

        var error = new Exception("Test Exception");
        _changeSetsSource.OnError(error);

        _itemsSource.AddOrUpdate(new DisposableObject(2));

        await Assert.That(_results.Error).IsEqualTo(error);
        await Assert.That(_itemsDisposedBeforeDownstreamNotifications).IsEmpty().Because("items should not be disposed until after downstream notifications are processed");
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("1 update was made to the source");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("1 item was not removed from the list");
        await Assert.That(_results.Data.Items.All(item => item.IsDisposed)).IsTrue().Because("items remaining in the list should be disposed");
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

        _itemsSource.AddOrUpdate(items);

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
