using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

                Console.WriteLine(aggregator.Data.Count);

                aggregator.Data.Count.Should().Be(100);


                parents[0].Children.Add(new Person("NewlyAddded",100));
                aggregator.Data.Count.Should().Be(101);

                //sut.Children.Count.Should().Be(5);
                //sut.Children.Items.ShouldBeEquivalentTo(parents.SelectMany(p => p.Children.Take(5)));

                ////add a child to the observable collection
                //parents[2].Children.Add(children[5]);

                //sut.Children.Count.Should().Be(6);
                //sut.Children.Items.ShouldBeEquivalentTo(parents.SelectMany(p => p.Children));

                ////remove a parent and check children have moved
                //source.RemoveKey(1);
                //sut.Children.Count.Should().Be(4);
                //sut.Children.Items.ShouldBeEquivalentTo(parents.Skip(1).SelectMany(p => p.Children));

                ////add a parent and check items have been added back in
                //source.AddOrUpdate(parents[0]);

                //sut.Children.Count.Should().Be(6);
                //sut.Children.Items.ShouldBeEquivalentTo(parents.SelectMany(p => p.Children));
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