using System;
using System.Linq;

using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.List;

public class AutoRefreshFixture
{
    [Fact]
    public void AutoRefresh()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).AsAggregator();
        list.AddRange(items);

        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(1);

        items[0].Age = 10;
        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(2);

        results.Messages[1].First().Reason.Should().Be(ListChangeReason.Refresh);

        //remove an item and check no change is fired
        var toRemove = items[1];
        list.Remove(toRemove);
        results.Data.Count.Should().Be(99);
        results.Messages.Count.Should().Be(3);
        toRemove.Age = 100;
        results.Messages.Count.Should().Be(3);

        //add it back in and check it updates
        list.Add(toRemove);
        results.Messages.Count.Should().Be(4);
        toRemove.Age = 101;
        results.Messages.Count.Should().Be(5);

        results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Refresh);
    }

    [Fact]
    public void AutoRefreshBatched()
    {
        var scheduler = new TestScheduler();

        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, 1)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age, TimeSpan.FromSeconds(1), scheduler: scheduler).AsAggregator();
        list.AddRange(items);

        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(1);

        //update 50 records
        items.Skip(50).ForEach(p => p.Age += 1);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);

        //should be another message with 50 refreshes
        results.Messages.Count.Should().Be(2);
        results.Messages[1].Refreshes.Should().Be(50);
    }

    [Fact]
    public void AutoRefreshDistinct()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).DistinctValues(p => p.Age / 10).AsAggregator();
        list.AddRange(items);

        results.Data.Count.Should().Be(11);
        results.Messages.Count.Should().Be(1);

        //update an item which did not match the filter and does so after change
        items[50].Age = 500;
        results.Data.Count.Should().Be(12);

        results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Add);
        results.Messages.Last().First().Item.Current.Should().Be(50);
    }

    [Fact]
    public void AutoRefreshFilter()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).Filter(p => p.Age > 50).AsAggregator();
        list.AddRange(items);

        results.Data.Count.Should().Be(50);
        results.Messages.Count.Should().Be(1);

        //update an item which did not match the filter and does so after change
        items[0].Age = 60;
        results.Data.Count.Should().Be(51);
        results.Messages.Count.Should().Be(2);
        results.Messages[1].First().Reason.Should().Be(ListChangeReason.Add);

        //check for removes
        items[0].Age = 21;
        results.Data.Count.Should().Be(50);
        results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Remove);
        items[0].Age = 60;

        //update an item which matched the filter and still does [refresh should have propagated]
        items[60].Age = 160;
        results.Data.Count.Should().Be(51);
        results.Messages.Count.Should().Be(5);
        results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Replace);

        //remove an item and check no change is fired
        var toRemove = items[65];
        list.Remove(toRemove);
        results.Data.Count.Should().Be(50);
        results.Messages.Count.Should().Be(6);
        toRemove.Age = 100;
        results.Messages.Count.Should().Be(6);

        //add it back in and check it updates
        list.Add(toRemove);
        results.Messages.Count.Should().Be(7);
        toRemove.Age = 101;
        results.Messages.Count.Should().Be(8);

        results.Messages.Last().First().Reason.Should().Be(ListChangeReason.Replace);
    }

    [Fact]
    public void AutoRefreshGroup()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).GroupOn(p => p.Age % 10).AsAggregator();
        void CheckContent()
        {
            foreach (var grouping in items.GroupBy(p => p.Age % 10))
            {
                var childGroup = results.Data.Items.Single(g => g.GroupKey == grouping.Key);
                var expected = grouping.OrderBy(p => p.Name);
                var actual = childGroup.List.Items.OrderBy(p => p.Name);
                actual.Should().BeEquivalentTo(expected);
            }
        }

        list.AddRange(items);
        results.Data.Count.Should().Be(10);
        results.Messages.Count.Should().Be(1);
        CheckContent();

        //move person from group 1 to 2
        items[0].Age = items[0].Age + 1;
        CheckContent();

        //change the value and move to a grouping which does not yet exist
        items[1].Age = -1;
        results.Data.Count.Should().Be(11);
        results.Data.Items[^1].GroupKey.Should().Be(-1);
        results.Data.Items[^1].List.Count.Should().Be(1);
        results.Data.Items[0].List.Count.Should().Be(9);
        CheckContent();

        //put the value back where it was and check the group was removed
        items[1].Age = 1;
        results.Data.Count.Should().Be(10);
        CheckContent();

        var groupOf3 = results.Data.Items.ElementAt(2);

        IChangeSet<Person>? changes = null;
        groupOf3.List.Connect().Subscribe(c => changes = c);

        //refresh an item which makes it belong to the same group - should then propagate a refresh
        items[2].Age = 13;
        changes.Should().NotBeNull();
        changes!.Count.Should().Be(1);
        changes!.First().Reason.Should().Be(ListChangeReason.Replace);
        changes!.First().Item.Current.Should().BeSameAs(items[2]);
    }

    [Fact]
    public void AutoRefreshGroupImmutable()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).GroupWithImmutableState(p => p.Age % 10).AsAggregator();
        void CheckContent()
        {
            foreach (var grouping in items.GroupBy(p => p.Age % 10))
            {
                var childGroup = results.Data.Items.Single(g => g.Key == grouping.Key);
                var expected = grouping.OrderBy(p => p.Name);
                var actual = childGroup.Items.OrderBy(p => p.Name);
                actual.Should().BeEquivalentTo(expected);
            }
        }

        list.AddRange(items);
        results.Data.Count.Should().Be(10);
        results.Messages.Count.Should().Be(1);
        CheckContent();

        //move person from group 1 to 2
        items[0].Age = items[0].Age + 1;
        CheckContent();

        //change the value and move to a grouping which does not yet exist
        items[1].Age = -1;
        results.Data.Count.Should().Be(11);
        results.Data.Items[^1].Key.Should().Be(-1);
        results.Data.Items[^1].Count.Should().Be(1);
        results.Data.Items[0].Count.Should().Be(9);
        CheckContent();

        //put the value back where it was and check the group was removed
        items[1].Age = 1;
        results.Data.Count.Should().Be(10);
        results.Messages.Count.Should().Be(4);
        CheckContent();

        //refresh an item which makes it belong to the same group - should then propagate a refresh
        items[2].Age = 13;
        CheckContent();

        results.Messages.Count.Should().Be(5);
    }

    [Fact]
    public void AutoRefreshSelected()
    {
        //test added as v6 broke unit test in DynamicData.Snippets
        var initialItems = Enumerable.Range(1, 10).Select(i => new SelectableItem(i)).ToArray();

        //result should only be true when all items are set to true
        using var sourceList = new SourceList<SelectableItem>();
        using var sut = sourceList.Connect().AutoRefresh().Filter(si => si.IsSelected).AsObservableList();
        sourceList.AddRange(initialItems);
        sut.Count.Should().Be(0);

        initialItems[0].IsSelected = true;
        sut.Count.Should().Be(1);

        initialItems[1].IsSelected = true;
        sut.Count.Should().Be(2);

        //remove the selected items
        sourceList.RemoveRange(0, 2);
        sut.Count.Should().Be(0);
    }

    [Fact]
    public void AutoRefreshSort()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).OrderByDescending(p => p.Age).ToArray();

        var comparer = SortExpressionComparer<Person>.Ascending(p => p.Age);

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).AsAggregator();
        void CheckOrder()
        {
            var sorted = items.OrderBy(p => p, comparer).ToArray();
            results.Data.Items.Should().BeEquivalentTo(sorted);
        }

        list.AddRange(items);

        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(1);
        CheckOrder();

        items[0].Age = 60;
        CheckOrder();
        results.Messages.Count.Should().Be(2);
        results.Messages.Last().Refreshes.Should().Be(1);
        results.Messages.Last().Moves.Should().Be(1);

        items[90].Age = -1; //move to beginning
        CheckOrder();
        results.Messages.Count.Should().Be(3);
        results.Messages.Last().Refreshes.Should().Be(1);
        results.Messages.Last().Moves.Should().Be(1);

        items[50].Age = 49; //same position so no move
        CheckOrder();
        results.Messages.Count.Should().Be(4);
        results.Messages.Last().Refreshes.Should().Be(1);
        results.Messages.Last().Moves.Should().Be(0);

        items[50].Age = 51; //same position so no move
        CheckOrder();
        results.Messages.Count.Should().Be(5);
        results.Messages.Last().Refreshes.Should().Be(1);
        results.Messages.Last().Moves.Should().Be(1);
    }

    [Fact]
    public void AutoRefreshTransform()
    {
        var items = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();

        //result should only be true when all items are set to true
        using var list = new SourceList<Person>();
        using var results = list.Connect().AutoRefresh(p => p.Age).Transform((p, idx) => new TransformedPerson(p, idx)).AsAggregator();
        list.AddRange(items);

        results.Data.Count.Should().Be(100);
        results.Messages.Count.Should().Be(1);

        //update an item which did not match the filter and does so after change
        items[0].Age = 60;
        results.Messages.Count.Should().Be(2);
        results.Messages.Last().Refreshes.Should().Be(1);
        results.Messages.Last().First().Item.Reason.Should().Be(ListChangeReason.Refresh);
        results.Messages.Last().First().Item.Current.Index.Should().Be(0);

        items[60].Age = 160;
        results.Messages.Count.Should().Be(3);
        results.Messages.Last().Refreshes.Should().Be(1);
        results.Messages.Last().First().Item.Reason.Should().Be(ListChangeReason.Refresh);
        results.Messages.Last().First().Item.Current.Index.Should().Be(60);
    }

    [Fact]
    public void RefreshTransformAsList()
    {
        var list = new SourceList<Example>();
        var valueList = list.Connect().AutoRefresh(e => e.Value).Transform(e => e.Value, true).AsObservableList();

        var obj = new Example { Value = 0 };
        list.Add(obj);
        obj.Value = 1;
        valueList.Items[0].Should().Be(1);
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
