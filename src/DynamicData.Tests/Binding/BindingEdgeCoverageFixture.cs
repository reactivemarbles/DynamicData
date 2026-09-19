#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DynamicData.Tests.Binding;

public sealed class BindingEdgeCoverageFixture
{
    [Test]
    public async Task ObservableCollectionChangeSetTreatsUnknownIndexAddAsAppendRange()
    {
        var source = new ManualNotifyCollection<int>();
        using var results = source.ToObservableChangeSet<ManualNotifyCollection<int>, int>().AsAggregator();

        source.AddRangeWithUnknownIndex(1, 2);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Last().Adds).IsEqualTo(2);
    }

    [Test]
    public async Task ObservableCollectionChangeSetRemovesUnknownIndexItemsByValue()
    {
        var source = new ManualNotifyCollection<int>(1, 2, 3);
        using var results = source.ToObservableChangeSet<ManualNotifyCollection<int>, int>().AsAggregator();

        source.RemoveWithUnknownIndex(2);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Last().Removes).IsEqualTo(1);
    }

    [Test]
    public async Task ObservableCollectionChangeSetRemovesKnownIndexRange()
    {
        var source = new ManualNotifyCollection<int>(1, 2, 3, 4);
        using var results = source.ToObservableChangeSet<ManualNotifyCollection<int>, int>().AsAggregator();

        source.RemoveRangeAt(1, 2);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Last().Removes).IsEqualTo(2);
    }

    [Test]
    public async Task ObservableCollectionChangeSetReplacesUnknownIndexItemByOldValue()
    {
        var source = new ManualNotifyCollection<int>(1, 2, 3);
        using var results = source.ToObservableChangeSet<ManualNotifyCollection<int>, int>().AsAggregator();

        source.ReplaceWithUnknownIndex(2, 20);

        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { 1, 20, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Messages.Last().Replaced).IsEqualTo(1);
    }

    [Test]
    public async Task ObservableCollectionChangeSetRejectsMoveWithoutOldIndex()
    {
        var source = new ManualNotifyCollection<int>(1, 2);
        Exception? error = null;

        using var subscription = source.ToObservableChangeSet<ManualNotifyCollection<int>, int>().Subscribe(_ => { }, ex => error = ex);
#if REACTIVE_TESTS
        source.RaiseMoveWithoutOldIndex(2, 0);
        await Assert.That(error).IsTypeOf<UnspecifiedIndexException>();
#else
        // Primitives invokes its accumulator inline; Rx routes its exception through OnError.
        await Assert.That(() => source.RaiseMoveWithoutOldIndex(2, 0)).Throws<UnspecifiedIndexException>();
        await Assert.That(error).IsNull();
#endif
    }

    [Test]
    public async Task ObserveCollectionChangesStopsAfterDisposal()
    {
        var source = new ObservableCollection<int>();
        var actions = new List<NotifyCollectionChangedAction>();

        var subscription = source.ObserveCollectionChanges().Subscribe(pattern => actions.Add(pattern.EventArgs.Action));
        source.Add(1);
        subscription.Dispose();
        source.Add(2);

        await Assert.That(actions).IsEquivalentTo(new[] { NotifyCollectionChangedAction.Add }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task WhenAnyPropertyChangedFiltersNamesAndStopsAfterDisposal()
    {
        var source = new NotifyingItem();
        var notifications = new List<NotifyingItem?>();

        var subscription = source.WhenAnyPropertyChanged(nameof(NotifyingItem.A)).Subscribe(notifications.Add);
        source.B = 1;
        source.A = 2;
        subscription.Dispose();
        source.A = 3;

        await Assert.That(notifications).IsEquivalentTo(new NotifyingItem?[] { source }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task WhenValueChangedSuppressesUnobtainableValueUntilPathExists()
    {
        var source = new NotifyingItem();
        using var subscription = source.WhenValueChanged(item => item.Child!.A).RecordValues(out var results);

        source.B = 1;
        source.Child = new NotifyingItem { A = 7 };
        source.Child.A = 8;

        await Assert.That(results.RecordedValues).IsEquivalentTo(new[] { 7, 8 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(results.Error).IsNull();
    }

    private sealed class ManualNotifyCollection<T> : Collection<T>, INotifyCollectionChanged
    {
        public ManualNotifyCollection(params T[] items)
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void AddRangeWithUnknownIndex(params T[] items)
        {
            foreach (var item in items)
            {
                Items.Add(item);
            }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, -1));
        }

        public void RemoveWithUnknownIndex(T item)
        {
            Items.Remove(item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, -1));
        }

        public void RemoveRangeAt(int index, int count)
        {
            var removed = Items.Skip(index).Take(count).ToArray();
            for (var i = 0; i < count; i++)
            {
                Items.RemoveAt(index);
            }

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, index));
        }

        public void ReplaceWithUnknownIndex(T oldItem, T newItem)
        {
            var index = Items.IndexOf(oldItem);
            Items[index] = newItem;

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, new[] { newItem }, new[] { oldItem }, -1));
        }

        public void RaiseMoveWithoutOldIndex(T item, int newIndex) =>
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, -1));

    }

    private sealed class NotifyingItem : INotifyPropertyChanged
    {
        private int _a;
        private int _b;
        private NotifyingItem? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int A
        {
            get => _a;
            set => SetField(ref _a, value, nameof(A));
        }

        public int B
        {
            get => _b;
            set => SetField(ref _b, value, nameof(B));
        }

        public NotifyingItem? Child
        {
            get => _child;
            set => SetField(ref _child, value, nameof(Child));
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
