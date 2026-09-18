#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class AutoRefreshFixture
{
    [Test]
    public async Task AutoRefresh()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        items[0].Age = 10;
        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(2);

        await Assert.That(results.Messages[1].First().Reason).IsEqualTo(ListChangeReason.Refresh);

        //remove an item and check no change is fired
        var toRemove = items[1];
        list.Remove(toRemove);
        await Assert.That(results.Data.Count).IsEqualTo(99);
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        toRemove.Age = 100;
        await Assert.That(results.Messages.Count).IsEqualTo(3);

        //add it back in and check it updates
        list.Add(toRemove);
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        toRemove.Age = 101;
        await Assert.That(results.Messages.Count).IsEqualTo(5);

        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Refresh);
    }

    [Test]
    public async Task AutoRefreshBatched()
    {
        var scheduler = new TestScheduler();

        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age, TimeSpan.FromSeconds(1), scheduler: scheduler).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        //update 50 records
        items.Skip(50).ForEach(p => p.Age += 1);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        //should be another message with 50 refreshes
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Refreshes).IsEqualTo(50);
    }

    [Test]
    public async Task AutoRefreshDistinct()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).DistinctValues(p => p.Age / 10).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(11);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        //update an item which did not match the filter and does so after change
        items[50].Age = 500;
        await Assert.That(results.Data.Count).IsEqualTo(12);

        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Add);
        await Assert.That(results.Messages.Last().First().Item.Current).IsEqualTo(50);
    }

    [Test]
    public async Task AutoRefreshFilter()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).Filter(p => p.Age > 50).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(50);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        //update an item which did not match the filter and does so after change
        items[0].Age = 60;
        await Assert.That(results.Data.Count).IsEqualTo(51);
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].First().Reason).IsEqualTo(ListChangeReason.Add);

        //check for removes
        items[0].Age = 21;
        await Assert.That(results.Data.Count).IsEqualTo(50);
        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Remove);
        items[0].Age = 60;

        //update an item which matched the filter and still does [refresh should have propagated]
        items[60].Age = 160;
        await Assert.That(results.Data.Count).IsEqualTo(51);
        await Assert.That(results.Messages.Count).IsEqualTo(5);
        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Refresh);

        //remove an item and check no change is fired
        var toRemove = items[65];
        list.Remove(toRemove);
        await Assert.That(results.Data.Count).IsEqualTo(50);
        await Assert.That(results.Messages.Count).IsEqualTo(6);
        toRemove.Age = 100;
        await Assert.That(results.Messages.Count).IsEqualTo(6);

        //add it back in and check it updates
        list.Add(toRemove);
        await Assert.That(results.Messages.Count).IsEqualTo(7);
        toRemove.Age = 101;
        await Assert.That(results.Messages.Count).IsEqualTo(8);

        await Assert.That(results.Messages.Last().First().Reason).IsEqualTo(ListChangeReason.Refresh);
    }

    [Test]
    public async Task AutoRefreshGroup()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).GroupOn(p => p.Age % 10).AsAggregator();
        async Task CheckContent()
        {
            foreach (var grouping in items.GroupBy(p => p.Age % 10))
            {
                var childGroup = results.Data.Items.Single(g => g.GroupKey == grouping.Key);
                var expected = grouping.OrderBy(p => p.Name);
                var actual = childGroup.List.Items.OrderBy(p => p.Name);
                await Assert.That(actual).IsEquivalentTo(expected);
            }
        }

        list.AddRange(items);
        await Assert.That(results.Data.Count).IsEqualTo(10);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await CheckContent();

        //move person from group 1 to 2
        items[0].Age = items[0].Age + 1;
        await CheckContent();

        //change the value and move to a grouping which does not yet exist
        items[1].Age = -1;
        await Assert.That(results.Data.Count).IsEqualTo(11);
        await Assert.That(results.Data.Items[^1].GroupKey).IsEqualTo(-1);
        await Assert.That(results.Data.Items[^1].List.Count).IsEqualTo(1);
        await Assert.That(results.Data.Items[0].List.Count).IsEqualTo(9);
        await CheckContent();

        //put the value back where it was and check the group was removed
        items[1].Age = 1;
        await Assert.That(results.Data.Count).IsEqualTo(10);
        await CheckContent();

        var groupOf3 = results.Data.Items.ElementAt(2);

        IChangeSet<Person>? changes = null;
        groupOf3.List.Connect().Subscribe(c => changes = c);

        //refresh an item which makes it belong to the same group - should then propagate a refresh
        items[2].Age = 13;
        await Assert.That(changes).IsNotNull();
        await Assert.That(changes!.Count).IsEqualTo(1);
        await Assert.That(changes!.First().Reason).IsEqualTo(ListChangeReason.Replace);
        await Assert.That(changes!.First().Item.Current).IsSameReferenceAs(items[2]);
    }

    [Test]
    public async Task AutoRefreshGroupImmutable()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).GroupWithImmutableState(p => p.Age % 10).AsAggregator();
        async Task CheckContent()
        {
            foreach (var grouping in items.GroupBy(p => p.Age % 10))
            {
                var childGroup = results.Data.Items.Single(g => g.Key == grouping.Key);
                var expected = grouping.OrderBy(p => p.Name);
                var actual = childGroup.Items.OrderBy(p => p.Name);
                await Assert.That(actual).IsEquivalentTo(expected);
            }
        }

        list.AddRange(items);
        await Assert.That(results.Data.Count).IsEqualTo(10);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await CheckContent();

        //move person from group 1 to 2
        items[0].Age = items[0].Age + 1;
        await CheckContent();

        //change the value and move to a grouping which does not yet exist
        items[1].Age = -1;
        await Assert.That(results.Data.Count).IsEqualTo(11);
        await Assert.That(results.Data.Items[^1].Key).IsEqualTo(-1);
        await Assert.That(results.Data.Items[^1].Count).IsEqualTo(1);
        await Assert.That(results.Data.Items[0].Count).IsEqualTo(9);
        await CheckContent();

        //put the value back where it was and check the group was removed
        items[1].Age = 1;
        await Assert.That(results.Data.Count).IsEqualTo(10);
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await CheckContent();

        //refresh an item which makes it belong to the same group - should then propagate a refresh
        items[2].Age = 13;
        await CheckContent();

        await Assert.That(results.Messages.Count).IsEqualTo(5);
    }

    [Test]
    public async Task AutoRefreshSelected()
    {
        //test added as v6 broke unit test in DynamicData.Snippets
        var initialItems = Enumerable.Range(1, 10).Select(i => new SelectableItem(i)).ToArray();

        //result should only be true when all items are set to true
        using var sourceList = new SourceList<SelectableItem>();
        using var sut = sourceList.Connect().AutoRefresh().Filter(si => si.IsSelected).AsObservableList();
        sourceList.AddRange(initialItems);
        await Assert.That(sut.Count).IsEqualTo(0);

        initialItems[0].IsSelected = true;
        await Assert.That(sut.Count).IsEqualTo(1);

        initialItems[1].IsSelected = true;
        await Assert.That(sut.Count).IsEqualTo(2);

        //remove the selected items
        sourceList.RemoveRange(0, 2);
        await Assert.That(sut.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AutoRefreshSort()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).OrderByDescending(p => p.Age).ToArray();

        var comparer = SortExpressionComparer<Person>.Ascending(p => p.Age);

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).AsAggregator();
        async Task CheckOrder()
        {
            var sorted = items.OrderBy(p => p, comparer).ToArray();
            await Assert.That(results.Data.Items).IsEquivalentTo(sorted);
        }

        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(1);
        await CheckOrder();

        items[0].Age = 60;
        await CheckOrder();
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages.Last().Moves).IsEqualTo(1);

        items[90].Age = -1; //move to beginning
        await CheckOrder();
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages.Last().Moves).IsEqualTo(1);

        items[50].Age = 49; //same position so no move
        await CheckOrder();
        await Assert.That(results.Messages.Count).IsEqualTo(4);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages.Last().Moves).IsEqualTo(0);

        items[50].Age = 51; //same position so no move
        await CheckOrder();
        await Assert.That(results.Messages.Count).IsEqualTo(5);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages.Last().Moves).IsEqualTo(1);
    }

    [Test]
    public async Task AutoRefreshTransform()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).Transform((p, idx) => new TransformedPerson(p, idx)).AsAggregator();
        list.AddRange(items);

        await Assert.That(results.Data.Count).IsEqualTo(100);
        await Assert.That(results.Messages.Count).IsEqualTo(1);

        //update an item which did not match the filter and does so after change
        items[0].Age = 60;
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages.Last().First().Item.Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(results.Messages.Last().First().Item.Current.Index).IsEqualTo(0);

        items[60].Age = 160;
        await Assert.That(results.Messages.Count).IsEqualTo(3);
        await Assert.That(results.Messages.Last().Refreshes).IsEqualTo(1);
        await Assert.That(results.Messages.Last().First().Item.Reason).IsEqualTo(ListChangeReason.Refresh);
        await Assert.That(results.Messages.Last().First().Item.Current.Index).IsEqualTo(60);
    }

    [Test]
    public async Task RefreshTransformAsList()
    {
        var list = new SourceList<Example>();
        var valueList = list.Connect().AutoRefresh(e => e.Value).Transform(e => e.Value, true).AsObservableList();

        var obj = new Example { Value = 0 };
        list.Add(obj);
        obj.Value = 1;
        await Assert.That(valueList.Items[0]).IsEqualTo(1);
    }

    private class Example : AbstractNotifyPropertyChanged
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetAndRaise(ref _value, value);
        }
    }

    private class SelectableItem(int id) : AbstractNotifyPropertyChanged
    {
        private bool _isSelected;

        public int Id { get; } = id;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(ref _isSelected, value);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((SelectableItem)obj);
        }

        public override int GetHashCode() => Id;

        protected bool Equals(SelectableItem other) => Id == other.Id;
    }

    private class TransformedPerson(Person person, int index)
    {
        public int Index { get; } = index;

        public Person Person { get; } = person;

        public DateTime TimeStamp { get; } = DateTime.Now;
    }
}
