#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif
using System.Collections.Specialized;
using System.Linq.Expressions;
using System.Reflection;
using ReactiveUI.Primitives.Signals;

namespace DynamicData.Tests.Binding;

public sealed class BindingBoundaryCoverageFixture
{
    [Test]
    public async Task SortExpressionComparerOrdersNullObjectsNullValuesAndDescendingTieBreakers()
    {
        var comparer = SortExpressionComparer<SortableBoundaryItem>.Ascending(PrimarySortValue)
            .ThenByDescending(static item => item.Secondary!);

        await Assert.That(comparer.Compare(null, null)).IsEqualTo(0);
        await Assert.That(comparer.Compare(null, new SortableBoundaryItem(1, "a"))).IsEqualTo(-1);
        await Assert.That(comparer.Compare(new SortableBoundaryItem(1, "a"), null)).IsEqualTo(1);
        await Assert.That(comparer.Compare(new SortableBoundaryItem(null, "a"), new SortableBoundaryItem(1, "a"))).IsEqualTo(-1);
        await Assert.That(comparer.Compare(new SortableBoundaryItem(1, "a"), new SortableBoundaryItem(null, "a"))).IsEqualTo(1);
        await Assert.That(comparer.Compare(new SortableBoundaryItem(1, "z"), new SortableBoundaryItem(1, "a"))).IsLessThan(0);
    }

    [Test]
    public async Task ObservableCollectionExtendedConstructorsAndRemoveRangePreserveItemsAndNotifications()
    {
        var fromList = new ObservableCollectionExtended<int>(new List<int> { 1, 2 });
        var fromEnumerable = new ObservableCollectionExtended<int>(Enumerable.Range(3, 2));
        var collection = new ObservableCollectionExtended<int>(new List<int> { 1, 2, 3, 4 });
        var actions = new List<NotifyCollectionChangedAction>();

        collection.CollectionChanged += (_, args) => actions.Add(args.Action);
        collection.RemoveRange(1, 2);

        await Assert.That(fromList).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fromEnumerable).IsEquivalentTo(new[] { 3, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(collection).IsEquivalentTo(new[] { 1, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(actions).IsEquivalentTo(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SinglePropertyObservationRoutesAccessorFailureToError()
    {
        var source = new ThrowingNode();
        Exception? error = null;

        using var subscription = source.WhenPropertyChanged(static node => node.Danger, notifyOnInitialValue: false)
            .Subscribe(_ => { }, ex => error = ex);

        source.ThrowOnDanger = true;
        source.RaiseDangerChanged();

        await Assert.That(error).IsTypeOf<InvalidOperationException>();
        await Assert.That(error!.Message).IsEqualTo("danger unavailable");
    }

    [Test]
    public async Task DeepPropertyObservationRoutesInitialAccessorFailureToError()
    {
        var source = new ThrowingNode
        {
            Child = new ThrowingNode { ThrowOnDanger = true }
        };
        Exception? error = null;

        using var subscription = source.WhenPropertyChanged(static node => node.Child!.Danger, notifyOnInitialValue: true)
            .Subscribe(_ => { }, ex => error = ex);

        await Assert.That(error).IsTypeOf<TargetInvocationException>();
        await Assert.That(error!.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(error.InnerException!.Message).IsEqualTo("danger unavailable");
    }

    [Test]
    public async Task BindingListAdaptorAddsUpdateWhenPreviousItemIsMissingAndRefreshesExistingItems()
    {
        var first = new SortableBoundaryItem(1, "first");
        var missingPrevious = new SortableBoundaryItem(99, "missing");
        var updated = new SortableBoundaryItem(2, "updated");
        var list = new BindingList<SortableBoundaryItem>();
        var adaptor = new BindingListAdaptor<SortableBoundaryItem, int>(list, refreshThreshold: 10);
        var notifications = new List<ListChangedType>();
        list.ListChanged += (_, args) => notifications.Add(args.ListChangedType);

        adaptor.Adapt(new ChangeSet<SortableBoundaryItem, int> { new(ChangeReason.Add, first.Id, first) });
        notifications.Clear();

        adaptor.Adapt(new ChangeSet<SortableBoundaryItem, int>
        {
            new(ChangeReason.Update, updated.Id, updated, ReactiveUI.Primitives.Optional<SortableBoundaryItem>.Create(missingPrevious))
        });
        adaptor.Adapt(new ChangeSet<SortableBoundaryItem, int> { new(ChangeReason.Refresh, first.Id, first) });

        await Assert.That(list).IsEquivalentTo(new[] { first, updated }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notifications).Contains(ListChangedType.ItemAdded);
        await Assert.That(notifications).Contains(ListChangedType.ItemChanged);
    }

    [Test]
    public async Task UnkeyedBindingListAdaptorAppliesIncrementalChangesAfterInitialLoad()
    {
        var list = new BindingList<int>();
        var adaptor = new BindingListAdaptor<int>(list, refreshThreshold: 10);
        var notifications = new List<ListChangedType>();
        list.ListChanged += (_, args) => notifications.Add(args.ListChangedType);

        adaptor.Adapt(new ChangeSet<int> { new(ListChangeReason.Add, 1) });
        notifications.Clear();
        adaptor.Adapt(new ChangeSet<int> { new(ListChangeReason.Add, 2) });

        await Assert.That(list).IsEquivalentTo(new[] { 1, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notifications).IsEquivalentTo(new[] { ListChangedType.ItemAdded }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortedBindingListAdaptorResetsWhenDataChangedBatchExceedsThreshold()
    {
        using var cache = new SourceCache<SortableBoundaryItem, int>(static item => item.Id);
        var list = new BindingList<SortableBoundaryItem>();
        var comparer = SortExpressionComparer<SortableBoundaryItem>.Ascending(PrimarySortValue);
        var notifications = new List<ListChangedType>();

        using var subscription = cache.Connect().Sort(comparer).Bind(list, resetThreshold: 1).Subscribe();
        list.ListChanged += (_, args) => notifications.Add(args.ListChangedType);

        cache.AddOrUpdate(new[]
        {
            new SortableBoundaryItem(3, "three"),
            new SortableBoundaryItem(1, "one"),
            new SortableBoundaryItem(2, "two")
        });

        notifications.Clear();
        cache.AddOrUpdate(new[]
        {
            new SortableBoundaryItem(5, "five"),
            new SortableBoundaryItem(4, "four")
        });

        await Assert.That(list.Select(static item => item.Id)).IsEquivalentTo(new[] { 1, 2, 3, 4, 5 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notifications).IsEquivalentTo(new[] { ListChangedType.Reset }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortedBindingListAdaptorMovesAndAddsMissingUpdateDuringReorderChanges()
    {
        var first = new SortableBoundaryItem(1, "one");
        var second = new SortableBoundaryItem(2, "two");
        var moved = new SortableBoundaryItem(3, "moved");
        var missingPrevious = new SortableBoundaryItem(99, "missing");
        var added = new SortableBoundaryItem(4, "added");
        var list = new BindingList<SortableBoundaryItem> { first, second, moved };
        var adaptor = new SortedBindingListAdaptor<SortableBoundaryItem, int>(list, refreshThreshold: 10);
        var changes = new SortedChangeSet<SortableBoundaryItem, int>(
            new KeyValueCollection<SortableBoundaryItem, int>(
                new[]
                {
                    new KeyValuePair<int, SortableBoundaryItem>(moved.Id, moved),
                    new KeyValuePair<int, SortableBoundaryItem>(first.Id, first),
                    new KeyValuePair<int, SortableBoundaryItem>(second.Id, second),
                    new KeyValuePair<int, SortableBoundaryItem>(added.Id, added)
                },
                new KeyValueComparer<SortableBoundaryItem, int>(),
                SortReason.Reorder,
                SortOptimisations.None),
            new ChangeSet<SortableBoundaryItem, int>
            {
                new(moved.Id, moved, 0, 2),
                new(ChangeReason.Update, added.Id, added, ReactiveUI.Primitives.Optional<SortableBoundaryItem>.Create(missingPrevious), 3, 3)
            });

        adaptor.Adapt(changes);

        await Assert.That(list.Select(static item => item.Id)).IsEquivalentTo(new[] { 3, 1, 2, 4 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortAndBindWithSchedulerResetsBindingListTargetsWithoutPerItemEvents()
    {
        using var cache = new SourceCache<SortableBoundaryItem, int>(static item => item.Id);
        var list = new BindingList<SortableBoundaryItem>();
        var comparer = SortExpressionComparer<SortableBoundaryItem>.Ascending(PrimarySortValue);
        var options = new SortAndBindOptions
        {
            ResetOnFirstTimeLoad = true,
            ResetThreshold = 1,
            Scheduler = Scheduler.Immediate
        };
        var notifications = new List<ListChangedType>();
        list.ListChanged += (_, args) => notifications.Add(args.ListChangedType);

        using var subscription = cache.Connect().SortAndBind(list, comparer, options).Subscribe();
        cache.AddOrUpdate(new[]
        {
            new SortableBoundaryItem(2, "two"),
            new SortableBoundaryItem(1, "one"),
            new SortableBoundaryItem(3, "three")
        });

        await Assert.That(list.Select(static item => item.Id)).IsEquivalentTo(new[] { 1, 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notifications).IsEquivalentTo(new[] { ListChangedType.Reset }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SortAndBindObservableComparerWithSchedulerSortsAfterComparerSignal()
    {
        using var cache = new SourceCache<SortableBoundaryItem, int>(static item => item.Id);
        using var comparer = new StateSignal<IComparer<SortableBoundaryItem>>(SortExpressionComparer<SortableBoundaryItem>.Ascending(PrimarySortValue));
        var list = new List<SortableBoundaryItem>();
        var options = new SortAndBindOptions { Scheduler = Scheduler.Immediate };

        using var subscription = cache.Connect().SortAndBind(list, comparer, options).Subscribe();
        cache.AddOrUpdate(new[]
        {
            new SortableBoundaryItem(2, "two"),
            new SortableBoundaryItem(1, "one"),
            new SortableBoundaryItem(3, "three")
        });
        comparer.OnNext(SortExpressionComparer<SortableBoundaryItem>.Descending(PrimarySortValue));

        await Assert.That(list.Select(static item => item.Id)).IsEquivalentTo(new[] { 3, 2, 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ExpressionBuilderHandlesNullPropertiesFieldsConversionsCallsAndInvalidBodies()
    {
        Expression<Func<ThrowingNode, int>>? nullExpression = null;
        Expression<Func<ThrowingNode, object>> convertedProperty = static node => node.Danger;
        Expression<Func<ThrowingNode, string?>> methodCall = static node => node.ToString();
        Expression<Func<ThrowingNode, int>> binary = static node => node.Danger + 1;
        Expression<Func<ThrowingNode, string>> field = static node => node.Field;

        var convertedMember = convertedProperty.GetMember();
        var method = methodCall.GetMember();
        var stepProperty = convertedProperty.SplitIntoSteps().OfType<MemberExpression>().Single().GetProperty();

        await Assert.That(convertedMember.Name).IsEqualTo(nameof(ThrowingNode.Danger));
        await Assert.That(method).IsTypeOf<MethodInfo>();
        await Assert.That(stepProperty.Name).IsEqualTo(nameof(ThrowingNode.Danger));
        await Assert.That(() => nullExpression!.GetMember()).Throws<ArgumentException>();
        await Assert.That(() => field.GetProperty()).Throws<ArgumentException>();
        await Assert.That(() => binary.GetMember()).Throws<ArgumentException>();
        await Assert.That(() => binary.SplitIntoSteps().ToArray()).Throws<ArgumentException>();
    }

    private static IComparable PrimarySortValue(SortableBoundaryItem item) => item.Primary.HasValue ? item.Primary.Value : null!;

    private sealed class SortableBoundaryItem
    {
        public SortableBoundaryItem(int id, string secondary)
            : this(id, id, secondary)
        {
        }

        public SortableBoundaryItem(int? primary, string? secondary)
            : this(primary ?? -1, primary, secondary)
        {
        }

        private SortableBoundaryItem(int id, int? primary, string? secondary)
        {
            Id = id;
            Primary = primary;
            Secondary = secondary;
        }

        public int Id { get; }

        public int? Primary { get; }

        public string? Secondary { get; }
    }

    private sealed class ThrowingNode : INotifyPropertyChanged
    {
        private ThrowingNode? _child;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Field = string.Empty;

        public ThrowingNode? Child
        {
            get => _child;
            set
            {
                if (ReferenceEquals(_child, value))
                {
                    return;
                }

                _child = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Child)));
            }
        }

        public bool ThrowOnDanger { get; set; }

        public int Danger
        {
            get
            {
                if (ThrowOnDanger)
                {
                    throw new InvalidOperationException("danger unavailable");
                }

                return 42;
            }
        }

        public void RaiseDangerChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Danger)));
    }
}
