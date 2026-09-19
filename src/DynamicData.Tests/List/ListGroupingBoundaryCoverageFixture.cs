namespace DynamicData.Tests.List;

public sealed class ListGroupingBoundaryCoverageFixture
{
    [Test]
    public async Task GroupOnHandlesRefreshRegroupReplaceClearAndDisposesRemovedGroups()
    {
        using var source = new TestSourceList<MutableItem>();
        using var regrouper = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        using var results = source.Connect().GroupOn(item => item.Group, regrouper).AsAggregator();

        var first = new MutableItem(1, "A", "one");
        var second = new MutableItem(2, "A", "two");
        var third = new MutableItem(3, "B", "three");
        source.AddRange(new[] { first, second, third });

        await AssertGroup(results.Data.Items, "A", first, second);
        await AssertGroup(results.Data.Items, "B", third);

        first.Group = "C";
        source.Refresh(0);

        await AssertGroup(results.Data.Items, "A", second);
        await AssertGroup(results.Data.Items, "B", third);
        await AssertGroup(results.Data.Items, "C", first);

        second.Group = "B";
        regrouper.OnNext(Unit.Default);

        await AssertGroupAbsent(results.Data.Items, "A");
        await AssertGroup(results.Data.Items, "B", third, second);
        await AssertGroup(results.Data.Items, "C", first);

        var replacement = new MutableItem(4, "B", "replacement");
        source.Replace(third, replacement);

        await AssertGroup(results.Data.Items, "B", replacement, second);

        source.Clear();

        await Assert.That(results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GroupWithImmutableStateReplacesSnapshotsForRefreshReplaceRegroupAndRemoval()
    {
        using var source = new TestSourceList<MutableItem>();
        using var regrouper = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        using var results = source.Connect().GroupWithImmutableState(item => item.Group, regrouper).AsAggregator();

        var first = new MutableItem(1, "A", "one");
        var second = new MutableItem(2, "A", "two");
        var third = new MutableItem(3, "B", "three");
        source.AddRange(new[] { first, second, third });

        await AssertImmutableGroup(results.Data.Items, "A", first, second);
        await AssertImmutableGroup(results.Data.Items, "B", third);

        first.Group = "C";
        source.Refresh(0);

        await AssertImmutableGroup(results.Data.Items, "A", second);
        await AssertImmutableGroup(results.Data.Items, "B", third);
        await AssertImmutableGroup(results.Data.Items, "C", first);

        second.Group = "B";
        regrouper.OnNext(Unit.Default);

        await AssertImmutableGroupAbsent(results.Data.Items, "A");
        await AssertImmutableGroup(results.Data.Items, "B", third, second);
        await AssertImmutableGroup(results.Data.Items, "C", first);

        var replacement = new MutableItem(4, "D", "replacement");
        source.Replace(third, replacement);

        await AssertImmutableGroup(results.Data.Items, "B", second);
        await AssertImmutableGroup(results.Data.Items, "D", replacement);

        source.Remove(second);

        await AssertImmutableGroupAbsent(results.Data.Items, "B");
        await AssertImmutableGroup(results.Data.Items, "C", first);
        await AssertImmutableGroup(results.Data.Items, "D", replacement);
    }

    [Test]
    public async Task DistinctValuesHandlesRangeRefreshReplaceRemoveRangeAndClearBoundaries()
    {
        using var source = new TestSourceList<MutableItem>();
        using var results = source.Connect().DistinctValues(item => item.Group).AsAggregator();

        var first = new MutableItem(1, "A", "one");
        var second = new MutableItem(2, "A", "two");
        var third = new MutableItem(3, "B", "three");
        source.AddRange(new[] { first, second, third });

        await Assert.That(results.Data.Items.OrderBy(static value => value)).IsEquivalentTo(new[] { "A", "B" });

        first.Group = "A";
        source.Refresh(0);

        first.Group = "C";
        source.Refresh(0);
        await Assert.That(results.Data.Items.OrderBy(static value => value)).IsEquivalentTo(new[] { "A", "B", "C" });

        source.Replace(third, new MutableItem(4, "C", "replacement"));
        await Assert.That(results.Data.Items.OrderBy(static value => value)).IsEquivalentTo(new[] { "A", "C" });

        source.RemoveMany(new[] { second, first });
        await Assert.That(results.Data.Items).IsEquivalentTo(new[] { "C" });

        source.Clear();
        await Assert.That(results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MergeManyCacheChangeSetsHandlesParentMoveReplaceClearAndChildRefresh()
    {
        using var parents = new SourceList<CacheParent>();
        var first = new CacheParent("first");
        var second = new CacheParent("second");
        var replacement = new CacheParent("replacement");
        first.Children.AddOrUpdate(new Child(1, "one", 10));
        second.Children.AddOrUpdate(new Child(2, "two", 20));

        using var results = parents.Connect().MergeManyChangeSets(parent => parent.Children.Connect(), Child.ValueEqualityComparer, Child.HighScoreComparer).AsAggregator();

        parents.AddRange(new[] { first, second });
        await Assert.That(results.Data.Count).IsEqualTo(2);

        parents.Move(0, 1);
        await Assert.That(results.Data.Count).IsEqualTo(2);

        replacement.Children.AddOrUpdate(new Child(3, "three", 30));
        parents.Replace(first, replacement);

        await Assert.That(results.Data.Items.Select(static child => child.Id).OrderBy(static id => id)).IsEquivalentTo(new[] { 2, 3 });

        second.Children.AddOrUpdate(new Child(2, "two-updated", 40));
        second.Children.Refresh(second.Children.Lookup(2).Value);

        await Assert.That(results.Data.Lookup(2).Value.Name).IsEqualTo("two-updated");

        parents.Clear();
        await Assert.That(results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MergeManyListChangeSetsHandlesParentMoveReplaceRemoveRangeAndChildUpdates()
    {
        using var parents = new SourceList<ListParent>();
        var first = new ListParent("first", new Child(1, "one", 10));
        var second = new ListParent("second", new Child(2, "two", 20));
        var third = new ListParent("third", new Child(3, "three", 30));
        using var results = parents.Connect().MergeManyChangeSets(parent => parent.Children.Connect(), Child.ValueEqualityComparer).AsAggregator();

        parents.AddRange(new[] { first, second, third });
        await Assert.That(results.Data.Items.Select(static child => child.Id).OrderBy(static id => id)).IsEquivalentTo(new[] { 1, 2, 3 });

        parents.Move(0, 2);
        await Assert.That(results.Data.Count).IsEqualTo(3);

        var replacement = new ListParent("replacement", new Child(4, "four", 40));
        parents.Replace(second, replacement);
        await Assert.That(results.Data.Items.Select(static child => child.Id).OrderBy(static id => id)).IsEquivalentTo(new[] { 1, 3, 4 });

        replacement.Children.Add(new Child(5, "five", 50));
        await Assert.That(results.Data.Items.Select(static child => child.Id).OrderBy(static id => id)).IsEquivalentTo(new[] { 1, 3, 4, 5 });

        parents.RemoveMany(new[] { first, third });
        await Assert.That(results.Data.Items.Select(static child => child.Id).OrderBy(static id => id)).IsEquivalentTo(new[] { 4, 5 });

        parents.Clear();
        await Assert.That(results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MergeManyChangeSetsPropagatesChildCompletionAndErrorAndDisposesRemovedParentSubscription()
    {
        using var parents = new SourceList<ManualParent>();
        var first = new ManualParent("first");
        var second = new ManualParent("second");
        var expected = new InvalidOperationException("child failed");
        using var results = parents.Connect().MergeManyChangeSets(parent => parent.Changes).AsAggregator();

        parents.Add(first);
        first.Emit(new ChangeSet<Child> { new(ListChangeReason.Add, new Child(1, "one", 10), 0) });
        await Assert.That(results.Data.Count).IsEqualTo(1);

        parents.Remove(first);
        await Assert.That(first.SubscriptionCount).IsEqualTo(0);
        first.Emit(new ChangeSet<Child> { new(ListChangeReason.Add, new Child(99, "stale", 99), 0) });
        await Assert.That(results.Data.Count).IsEqualTo(0);

        parents.Add(second);
        second.Complete();
        await Assert.That(results.IsCompleted).IsFalse();

        var third = new ManualParent("third");
        parents.Add(third);
        third.Fail(expected);
        await Assert.That(results.Exception).IsEqualTo(expected);
    }

    private static async Task AssertGroup(IEnumerable<IGroup<MutableItem, string>> groups, string key, params MutableItem[] expected)
    {
        var group = groups.SingleOrDefault(group => group.GroupKey == key);
        await Assert.That(group).IsNotNull();
        await Assert.That(group!.List.Items).IsEquivalentTo(expected);
    }

    private static async Task AssertGroupAbsent(IEnumerable<IGroup<MutableItem, string>> groups, string key) =>
        await Assert.That(groups.Any(group => group.GroupKey == key)).IsFalse();

#if REACTIVE_TESTS
    private static async Task AssertImmutableGroup(IEnumerable<DynamicData.Reactive.List.IGrouping<MutableItem, string>> groups, string key, params MutableItem[] expected)
#else
    private static async Task AssertImmutableGroup(IEnumerable<DynamicData.List.IGrouping<MutableItem, string>> groups, string key, params MutableItem[] expected)
#endif
    {
        var group = groups.SingleOrDefault(group => group.Key == key);
        await Assert.That(group).IsNotNull();
        await Assert.That(group!.Items).IsEquivalentTo(expected);
        await Assert.That(group.Count).IsEqualTo(expected.Length);
    }

#if REACTIVE_TESTS
    private static async Task AssertImmutableGroupAbsent(IEnumerable<DynamicData.Reactive.List.IGrouping<MutableItem, string>> groups, string key) =>
#else
    private static async Task AssertImmutableGroupAbsent(IEnumerable<DynamicData.List.IGrouping<MutableItem, string>> groups, string key) =>
#endif
        await Assert.That(groups.Any(group => group.Key == key)).IsFalse();

    private sealed class MutableItem(int id, string group, string name)
    {
        public int Id { get; } = id;

        public string Group { get; set; } = group;

        public string Name { get; } = name;

        public override string ToString() => $"{Id}:{Name}:{Group}";
    }

    private sealed class CacheParent(string name) : IDisposable
    {
        public string Name { get; } = name;

        public SourceCache<Child, int> Children { get; } = new(static child => child.Id);

        public void Dispose() => Children.Dispose();
    }

    private sealed class ListParent : IDisposable
    {
        public ListParent(string name, params Child[] children)
        {
            Name = name;
            Children.AddRange(children);
        }

        public string Name { get; }

        public SourceList<Child> Children { get; } = new();

        public void Dispose() => Children.Dispose();

        public override string ToString() => Name;
    }

    private sealed class ManualParent
    {
        private readonly IObservable<IChangeSet<Child>> _changes;
        private IObserver<IChangeSet<Child>>? _observer;

        public ManualParent(string name)
        {
            Name = name;
            _changes = Observable.Create<IChangeSet<Child>>(observer =>
            {
                _observer = observer;
                SubscriptionCount++;
                return Disposable.Create(() =>
                {
                    SubscriptionCount--;
                    if (ReferenceEquals(_observer, observer))
                    {
                        _observer = null;
                    }
                });
            });
        }

        public string Name { get; }

        public int SubscriptionCount { get; private set; }

        public IObservable<IChangeSet<Child>> Changes => _changes;

        public void Complete() => _observer?.OnCompleted();

        public void Emit(IChangeSet<Child> changes) => _observer?.OnNext(changes);

        public void Fail(Exception error) => _observer?.OnError(error);
    }

    private sealed class Child(int id, string name, int score)
    {
        public static IEqualityComparer<Child> ValueEqualityComparer { get; } = new ChildValueEqualityComparer();

        public static IComparer<Child> HighScoreComparer { get; } = Comparer<Child>.Create(static (left, right) => right.Score.CompareTo(left.Score));

        public int Id { get; } = id;

        public string Name { get; } = name;

        public int Score { get; } = score;

        public override string ToString() => $"{Id}:{Name}:{Score}";

        private sealed class ChildValueEqualityComparer : IEqualityComparer<Child>
        {
            public bool Equals(Child? x, Child? y) => x?.Id == y?.Id && x?.Name == y?.Name && x?.Score == y?.Score;

            public int GetHashCode(Child obj) => HashCode.Combine(obj.Id, obj.Name, obj.Score);
        }
    }
}
