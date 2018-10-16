using System.Collections.Generic;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{
    public class TransformManyObservableListFixture
    {
        [Fact]
        public void FlattenObservableList()
        {
            var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            int childIndex = 0;
            var parents = Enumerable.Range(1, 50)
                .Select(i =>
                {
                    var parent = new Parent(i, new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                    childIndex = childIndex + 2;
                    return parent;
                }).ToArray();


            using (var source = new SourceList<Parent>())
            using (var aggregator = source.Connect()
                .TransformMany(p => p.Children)
                .AsAggregator())
            {
                source.AddRange(parents);

                aggregator.Data.Count.Should().Be(100);

                //add a child to an observable collection and check the new item is added
                parents[0].Children.Add(new Person("NewlyAddded", 100));
                aggregator.Data.Count.Should().Be(101);

                ////remove first parent and check children have gone
                source.RemoveAt(0);
                aggregator.Data.Count.Should().Be(98);

                //check items can be cleared and then added back in
                var childrenInZero = parents[1].Children.Items.ToArray();
                parents[1].Children.Clear();
                aggregator.Data.Count.Should().Be(96);
                parents[1].Children.AddRange(childrenInZero);
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                var replacedChild = parents[1].Children.Items.First();
                var replacement = new Person("Replacement", 100);
                parents[1].Children.Replace(replacedChild, replacement);
                aggregator.Data.Count.Should().Be(98);

                aggregator.Data.Items.Contains(replacement).Should().BeTrue();
                aggregator.Data.Items.Contains(replacedChild).Should().BeFalse();
            }
        }

        [Fact]
        public void FlattenReadOnlyObservableList()
        {
            var children = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();

            int childIndex = 0;
            var parents = Enumerable.Range(1, 50)
                .Select(i =>
                {
                    var parent = new Parent(i, new[]
                    {
                        children[childIndex],
                        children[childIndex + 1]
                    });

                    childIndex = childIndex + 2;
                    return parent;
                }).ToArray();


            using (var source = new SourceList<Parent>())
            using (var aggregator = source.Connect()
                .TransformMany(p => p.ChildrenReadonly)
                .AsAggregator())
            {
                source.AddRange(parents);

                aggregator.Data.Count.Should().Be(100);

                //add a child to an observable collection and check the new item is added
                parents[0].Children.Add(new Person("NewlyAddded", 100));
                aggregator.Data.Count.Should().Be(101);

                ////remove first parent and check children have gone
                source.RemoveAt(0);
                aggregator.Data.Count.Should().Be(98);


                //check items can be cleared and then added back in
                var childrenInZero = parents[1].Children.Items.ToArray();
                parents[1].Children.Clear();
                aggregator.Data.Count.Should().Be(96);
                parents[1].Children.AddRange(childrenInZero);
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                var replacedChild = parents[1].Children.Items.First();
                var replacement = new Person("Replacement", 100);
                parents[1].Children.Replace(replacedChild, replacement);
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                aggregator.Data.Items.Contains(replacement).Should().BeTrue();
                aggregator.Data.Items.Contains(replacedChild).Should().BeFalse();
            }
        }

        [Fact]
        public void ObservableListWithoutInitialData()
        {
            using (var parents = new SourceList<Parent>())
            {

                var collection = parents.Connect()
                    .TransformMany(d => d.Children)
                    .AsObservableList();

                var parent = new Parent();
                parents.Add(parent);

                collection.Count.Should().Be(0);

                parent.Children.Add(new Person("child1", 1));
                collection.Count.Should().Be(1);

                parent.Children.Add(new Person("child2", 2));
                collection.Count.Should().Be(2);
            }
        }

        [Fact]
        public void ReadOnlyObservableListWithoutInitialData()
        {
            using (var parents = new SourceList<Parent>())
            {
                var collection = parents.Connect()
                    .TransformMany(d => d.ChildrenReadonly)
                    .AsObservableList();

                var parent = new Parent();
                parents.Add(parent);

                collection.Count.Should().Be(0);

                parent.Children.Add(new Person("child1", 1));
                collection.Count.Should().Be(1);

                parent.Children.Add(new Person("child2", 2));
                collection.Count.Should().Be(2);
            }
        }

        private class Parent
        {
            public SourceList<Person> Children;
            public IObservableList<Person> ChildrenReadonly { get; }

            public int Id { get; }

            public Parent(int id, params Person[] children) : this()
            {
                Id = id;
                Children.AddRange(children);                
            }

            public Parent()
            {
                Children = new SourceList<Person>();
                ChildrenReadonly = Children.AsObservableList();
            }
        }
    }
}
