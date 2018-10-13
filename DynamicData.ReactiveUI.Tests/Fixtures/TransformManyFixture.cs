using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.ReactiveUI.Tests.Domain;
using DynamicData.Tests;
using FluentAssertions;
using ReactiveUI;
using ReactiveUI.Legacy;
using Xunit;

namespace DynamicData.ReactiveUI.Tests.Fixtures
{
    public class TransformManyFixture
    {
        [Fact]
        public void TransformMany()
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
        public void TransformManyWithKey()
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


            using (var source = new SourceCache<Parent, int>(x => x.Id))
            using (var aggregator = source.Connect()
                .TransformMany(p => p.Children, c => c.Name)
                .AsAggregator())
            {
                source.AddOrUpdate(parents);

                aggregator.Data.Count.Should().Be(100);

                //add a child to an observable collection and check the new item is added
                parents[0].Children.Add(new Person("NewlyAddded", 100));
                aggregator.Data.Count.Should().Be(101);

                ////remove first parent and check children have gone
                source.RemoveKey(1);
                aggregator.Data.Count.Should().Be(98);


                //check items can be cleared and then added back in
                var childrenInZero = parents[1].Children.ToArray();
                parents[1].Children.Clear();
                aggregator.Data.Count.Should().Be(96);
                parents[1].Children.AddRange(childrenInZero);
                aggregator.Data.Count.Should().Be(98);

                //replace produces an update
                var replacedChild = parents[1].Children[0];
                parents[1].Children[0] = new Person("Replacement", 100);
                aggregator.Data.Count.Should().Be(98);

                aggregator.Data.Lookup(replacedChild.Name).HasValue.Should().BeFalse();
                aggregator.Data.Lookup("Replacement").HasValue.Should().BeTrue();
            }
        }

        [Fact]
        [Trait("Performance", "Manual run only")]
        public void Perf()
        {
            var children = Enumerable.Range(1, 10000).Select(i => new Person("Name" + i, i)).ToArray();

            int childIndex = 0;
            var parents = Enumerable.Range(1, 5000)
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



            var sw = new Stopwatch();

            using (var source = new SourceCache<Parent, int>(x => x.Id))
            using (var sut = source.Connect()
                .Do(_ => sw.Start())
                .TransformMany(p => p.Children, c => c.Name)
                .Do(_ => sw.Stop())
                .Subscribe(c => Console.WriteLine($"Changes = {c.Count:N0}")))
            {
                source.AddOrUpdate(parents);
                Console.WriteLine($"{sw.ElapsedMilliseconds}");
            }
        }


        private class Parent
        {
            public int Id { get; }
            public ReactiveList<Person> Children { get; }

            public Parent(int id, IEnumerable<Person> children)
            {
                Id = id;
                Children = new ReactiveList<Person>(children);
            }
        }
    }
}
