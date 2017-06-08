using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class TransformManyObservableCacheFixture
    {
        [Test]
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


            using (var source = new SourceCache<Parent, int>(x => x.Id))
            using (var aggregator = source.Connect()
                .TransformMany(p=>p.Children, c=>c.Name)
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
                parents[1].Children[0] = new Person("Replacement",100);
                aggregator.Data.Count.Should().Be(98);

                aggregator.Data.Lookup(replacedChild.Key).HasValue.Should().BeFalse();
                aggregator.Data.Lookup("Replacement").HasValue.Should().BeTrue();
            }
        }

        [Test]
        [Ignore("Manual run only")]
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
            using (var sub = source.Connect()
                .Do(_ => sw.Start())
                .TransformMany(p => p.Children, c => c.Name)
                .Do(_ => sw.Stop())
                .Subscribe())
            {
                source.AddOrUpdate(parents);
                Console.WriteLine(sw.ElapsedMilliseconds);
            }
        }


        private class Parent
        {
            public int Id { get; }
            public ObservableCollection<Person> Children { get; }

            public Parent(int id, IEnumerable<Person> children)
            {
                Id = id;
                Children = new ObservableCollection<Person>(children);
            }
        }



    }
}