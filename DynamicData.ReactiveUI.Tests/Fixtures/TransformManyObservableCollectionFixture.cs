using System.Collections.Generic;
using System.Linq;
using DynamicData.ReactiveUI.Tests.Domain;
using DynamicData.Tests;
using FluentAssertions;
using ReactiveUI.Legacy;
using Xunit;

#pragma warning disable CS0618 // Using legacy code.

namespace DynamicData.ReactiveUI.Tests.Fixtures
{
    
    public class TransformManyObservableCollectionFixture
    {
        [Fact]
        public void FlattenObservableCollection()
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
            using (var aggregator = source.Connect().TransformMany(p => p.Children)
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
                var childrenInZero = parents[1].Children.ToArray();
                parents[1].Children.Clear();
                aggregator.Data.Count.Should().Be(96);
                parents[1].Children.AddRange(childrenInZero);
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                var replacedChild = parents[1].Children[0];
                var replacement = new Person("Replacement", 100);
                parents[1].Children[0] = replacement;
                aggregator.Data.Count.Should().Be(98);

               aggregator.Data.Items.Contains(replacement).Should().BeTrue();
               aggregator.Data.Items.Contains(replacedChild).Should().BeFalse();
            }
        }

        [Fact]
        public void FlattenReadOnlyObservableCollection()
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
                var childrenInZero = parents[1].Children.ToArray();
                parents[1].Children.Clear();
                aggregator.Data.Count.Should().Be(96);
                parents[1].Children.AddRange(childrenInZero);
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                var replacedChild = parents[1].Children[0];
                var replacement = new Person("Replacement", 100);
                parents[1].Children[0] = replacement;
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                aggregator.Data.Items.Contains(replacement).Should().BeTrue();
                aggregator.Data.Items.Contains(replacedChild).Should().BeFalse();
            }
        }

        [Fact]
        public void ObservableCollectionWithoutInitialData()
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
        public void ReadOnlyObservableCollectionWithoutInitialData()
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
            public ReactiveList<Person> Children { get; }
            public IReadOnlyReactiveList<Person> ChildrenReadonly { get; }

            public Parent(int id, IEnumerable<Person> children)
            {
                Children = new ReactiveList<Person>(children);
                ChildrenReadonly = Children;
            }

            public Parent()
            {
                Children = new ReactiveList<Person>();
                ChildrenReadonly = Children;
            }
        }
    }
}
