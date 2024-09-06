using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class DisposeManyFixture : IDisposable
{
    private readonly Subject<IChangeSet<DisposableObject, int>> _changeSetsSource;

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
                        change.Current.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");

                        if (change.Previous.HasValue)
                            change.Previous.Value.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");
                    }
                },
                onError: _ =>
                {
                    foreach(var item in _itemsSource.Items)
                        item.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");
                },
                onCompleted: () =>
                {
                    foreach(var item in _itemsSource.Items)
                        item.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");
                }));
    }

    public void Dispose()
    {
        _changeSetsSource.Dispose();
        _itemsSource.Dispose();
        _results.Dispose();
    }

    [Fact]
    // Verifies https://github.com/reactivemarbles/DynamicData/issues/668
    public void ErrorsArePropagated()
    {
        var error = new Exception("Test Exception");

        var source = Observable.Throw<IChangeSet<object, object>>(error)
            .DisposeMany();

        FluentActions.Invoking(() => source.Subscribe()).Should().Throw<Exception>().Which.Should().BeSameAs(error);

        var receivedError = null as Exception;
        source.Subscribe(
            onNext: static _ => { },
            onError: error => receivedError = error);
        receivedError.Should().BeSameAs(error);
    }

    [Fact]
    public void ItemsAreDisposedAfterRemovalOrReplacement()
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

        _results.Error.Should().BeNull();
        _results.Messages.Count.Should().Be(10, "10 updates were made to the source");
        _results.Data.Count.Should().Be(3, "3 items were not removed from the list");
        _results.Data.Items.All(item => item.IsDisposed).Should().BeFalse("items remaining in the list should not be disposed");
        items.Except(_results.Data.Items).All(item => item.IsDisposed).Should().BeTrue("items removed from the list should be disposed");
    }

    [Fact]
    public void RemainingItemsAreDisposedAfterCompleted()
    {
        _itemsSource.AddOrUpdate(new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3)
        });

        _itemsSource.Dispose();
        _changeSetsSource.OnCompleted();

        _results.Error.Should().BeNull();
        _results.Messages.Count.Should().Be(1, "1 update was made to the source");
        _results.Data.Count.Should().Be(3, "3 items were not removed from the list");
        _results.Data.Items.All(item => item.IsDisposed).Should().BeTrue("Items remaining in the list should be disposed");
    }

    [Fact]
    public void RemainingItemsAreDisposedAfterError()
    {
        _itemsSource.AddOrUpdate(new DisposableObject(1));
        
        var error = new Exception("Test Exception");
        _changeSetsSource.OnError(error);

        _itemsSource.AddOrUpdate(new DisposableObject(2));

        _results.Error.Should().Be(error);
        _results.Messages.Count.Should().Be(1, "1 update was made to the source");
        _results.Data.Count.Should().Be(1, "1 item was not removed from the list");
        _results.Data.Items.All(item => item.IsDisposed).Should().BeTrue("items remaining in the list should be disposed");
    }

    [Fact]
    public void RemainingItemsAreDisposedAfterUnsubscription()
    {
        var items = new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3)
        };

        _itemsSource.AddOrUpdate(items);

        _results.Dispose();

        items.All(item => item.IsDisposed).Should().BeTrue("Items remaining in the list should be disposed");
    }

    private class DisposableObject(int id) : IDisposable
    {
        public int Id { get; private set; } = id;

        public bool IsDisposed { get; private set; }

        public void Dispose()
            => IsDisposed = true;
    }
}
