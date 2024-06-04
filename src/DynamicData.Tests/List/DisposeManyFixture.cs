using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public sealed class DisposeManyFixture : IDisposable
{
    private readonly Subject<IChangeSet<DisposableObject>> _changeSetsSource;

    private readonly SourceList<DisposableObject> _itemsSource;

    private readonly ChangeSetAggregator<DisposableObject> _results;

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
                            change.Item.Current.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");

                        if (change.Item.Previous.HasValue)
                            change.Item.Previous.Value.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");

                        if (change.Range is not null)
                            foreach (var item in change.Range)
                                item.IsDisposed.Should().BeFalse("items should not be disposed until after downstream notifications are processed");
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

        var source = Observable.Throw<IChangeSet<object>>(error)
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

        _results.Exception.Should().BeNull();
        _results.Messages.Count.Should().Be(11, "11 updates were made to the source");
        _results.Data.Count.Should().Be(3, "3 items were not removed from the list");
        _results.Data.Items.All(item => item.IsDisposed).Should().BeFalse("items remaining in the list should not be disposed");
        items.Except(_results.Data.Items).All(item => item.IsDisposed).Should().BeTrue("items removed from the list should be disposed");
    }

    [Fact]
    public void RemainingItemsAreDisposedAfterCompleted()
    {
        _itemsSource.AddRange(new[]
        {
            new DisposableObject(1),
            new DisposableObject(2),
            new DisposableObject(3),
        });
        _itemsSource.Dispose();
        _changeSetsSource.OnCompleted();

        _results.Exception.Should().BeNull();
        _results.Messages.Count.Should().Be(1, "1 update was made to the list");
        _results.Data.Count.Should().Be(3, "3 items were not removed from the list");
        _results.Data.Items.All(item => item.IsDisposed).Should().BeTrue("items remaining in the list should be disposed");
    }

    [Fact]
    public void RemainingItemsAreDisposedAfterError()
    {
        _itemsSource.Add(new(1));
        
        var error = new Exception("Test Exception");
        _changeSetsSource.OnError(error);

        _itemsSource.Add(new(2));

        _results.Exception.Should().Be(error);
        _results.Messages.Count.Should().Be(1, "1 update was made to the list");
        _results.Data.Count.Should().Be(1, "1 item was not removed from the list");
        _results.Data.Items.All(item => item.IsDisposed).Should().BeTrue("Items remaining in the list should be disposed");
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

        _itemsSource.AddRange(items);

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
