using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class TransformManyObservableCollectionFixture
{
    [Test]
    public async Task FlattenObservableCollection()
    {
        var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        var childIndex = 0;
        var parents = Enumerable.Range(1, 50).Select(
            i =>
            {
                var parent = new Parent(
                    i,
                    new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                childIndex += 2;
                return parent;
            }).ToArray();

        using var source = new SourceList<Parent>();
        using var aggregator = source.Connect().TransformMany(p => p.Children).AsAggregator();
        source.AddRange(parents);

        await Assert.That(aggregator.Data.Count).IsEqualTo(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        await Assert.That(aggregator.Data.Count).IsEqualTo(101);

        ////remove first parent and check children have gone
        source.RemoveAt(0);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        await Assert.That(aggregator.Data.Count).IsEqualTo(96);
        parents[1].Children.AddRange(childrenInZero);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        var replacement = new Person("Replacement", 100);
        parents[1].Children[0] = replacement;
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        await Assert.That(aggregator.Data.Items.Contains(replacement)).IsTrue();
        await Assert.That(aggregator.Data.Items.Contains(replacedChild)).IsFalse();
    }

    [Test]
    public async Task FlattenObservableList()
    {
        var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        var childIndex = 0;
        var parents = Enumerable.Range(1, 50).Select(
            i =>
            {
                var parent = new ParentDynamic(
                    i,
                    new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                childIndex += 2;
                return parent;
            }).ToArray();

        using var source = new SourceList<ParentDynamic>();
        using var aggregator = source.Connect().TransformMany(p => p.ChildrenObservable).AsAggregator();
        T at<T>(IEnumerable<T> elements, int i) => elements.Skip(i).First();

        source.AddRange(parents);

        await Assert.That(aggregator.Data.Count).IsEqualTo(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        await Assert.That(aggregator.Data.Count).IsEqualTo(101);

        ////remove first parent and check children have gone
        source.RemoveAt(0);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.Items.ToArray();
        parents[1].Children.Clear();
        await Assert.That(aggregator.Data.Count).IsEqualTo(96);
        parents[1].Children.AddRange(childrenInZero);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        var replacedChild = at(parents[1].Children.Items, 0);
        var replacement = new Person("Replacement", 100);
        parents[1].Children.ReplaceAt(0, replacement);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        await Assert.That(aggregator.Data.Items.Contains(replacement)).IsTrue();
        await Assert.That(aggregator.Data.Items.Contains(replacedChild)).IsFalse();
    }

    [Test]
    public async Task FlattenReadOnlyObservableCollection()
    {
        var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

        var childIndex = 0;
        var parents = Enumerable.Range(1, 50).Select(
            i =>
            {
                var parent = new Parent(
                    i,
                    new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                childIndex += 2;
                return parent;
            }).ToArray();

        using var source = new SourceList<Parent>();
        using var aggregator = source.Connect().TransformMany(p => p.ChildrenReadonly).AsAggregator();
        source.AddRange(parents);

        await Assert.That(aggregator.Data.Count).IsEqualTo(100);

        //add a child to an observable collection and check the new item is added
        parents[0].Children.Add(new Person("NewlyAddded", 100));
        await Assert.That(aggregator.Data.Count).IsEqualTo(101);

        ////remove first parent and check children have gone
        source.RemoveAt(0);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //check items can be cleared and then added back in
        var childrenInZero = parents[1].Children.ToArray();
        parents[1].Children.Clear();
        await Assert.That(aggregator.Data.Count).IsEqualTo(96);
        parents[1].Children.AddRange(childrenInZero);
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        var replacedChild = parents[1].Children[0];
        var replacement = new Person("Replacement", 100);
        parents[1].Children[0] = replacement;
        await Assert.That(aggregator.Data.Count).IsEqualTo(98);

        //replace produces an update
        await Assert.That(aggregator.Data.Items.Contains(replacement)).IsTrue();
        await Assert.That(aggregator.Data.Items.Contains(replacedChild)).IsFalse();
    }

    [Test]
    public async Task ObservableCollectionWithoutInitialData()
    {
        using var parents = new SourceList<Parent>();
        var collection = parents.Connect().TransformMany(d => d.Children).AsObservableList();

        var parent = new Parent();
        parents.Add(parent);

        await Assert.That(collection.Count).IsEqualTo(0);

        parent.Children.Add(new Person("child1", 1));
        await Assert.That(collection.Count).IsEqualTo(1);

        parent.Children.Add(new Person("child2", 2));
        await Assert.That(collection.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ObservableListWithoutInitialData()
    {
        using var parents = new SourceList<ParentDynamic>();
        var collection = parents.Connect().TransformMany(d => d.ChildrenObservable).AsObservableList();

        var parent = new ParentDynamic();
        parents.Add(parent);

        await Assert.That(collection.Count).IsEqualTo(0);

        parent.Children.Add(new Person("child1", 1));
        await Assert.That(collection.Count).IsEqualTo(1);

        parent.Children.Add(new Person("child2", 2));
        await Assert.That(collection.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadOnlyObservableCollectionWithoutInitialData()
    {
        using var parents = new SourceList<Parent>();
        var collection = parents.Connect().TransformMany(d => d.ChildrenReadonly).AsObservableList();

        var parent = new Parent();
        parents.Add(parent);

        await Assert.That(collection.Count).IsEqualTo(0);

        parent.Children.Add(new Person("child1", 1));
        await Assert.That(collection.Count).IsEqualTo(1);

        parent.Children.Add(new Person("child2", 2));
        await Assert.That(collection.Count).IsEqualTo(2);
    }

    private class Parent
    {
        public Parent(int id, IEnumerable<Person> children)
        {
            Children = new ObservableCollection<Person>(children);
            ChildrenReadonly = new ReadOnlyObservableCollection<Person>(Children);
        }

        public Parent()
        {
            Children = new ObservableCollection<Person>();
            ChildrenReadonly = new ReadOnlyObservableCollection<Person>(Children);
        }

        public ObservableCollection<Person> Children { get; }

        public ReadOnlyObservableCollection<Person> ChildrenReadonly { get; }
    }

    private class ParentDynamic
    {
        public ParentDynamic(int id, IEnumerable<Person> children)
        {
            Children = new SourceList<Person>();
            Children.AddRange(children);
            ChildrenObservable = Children.AsObservableList();
        }

        public ParentDynamic()
        {
            Children = new SourceList<Person>();
            ChildrenObservable = Children.AsObservableList();
        }

        public SourceList<Person> Children { get; }

        public IObservableList<Person> ChildrenObservable { get; }
    }
}
