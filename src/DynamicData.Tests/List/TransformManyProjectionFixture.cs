using DynamicData.Aggregation;
using DynamicData.Binding;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Xunit;

namespace DynamicData.Tests.List
{
    public class TransformManyProjectionFixture : IDisposable
    {
        private readonly ISourceList<ClassWithNestedObservableCollection> _source;
        private readonly IObservableList<ProjectedNestedChild> _results;

        public TransformManyProjectionFixture()
        {
            _source = new SourceList<ClassWithNestedObservableCollection>();

            _results = _source.Connect()
                .AutoRefreshOnObservable(self => self.Children.ToObservableChangeSet())
                .TransformMany(parent => parent.Children.Select(c => new ProjectedNestedChild(parent, c)), new ProjectNestedChildEqualityComparer())
                .AsObservableList();
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
        public void AddRange()
        {
            var children = new[]
            {
                new NestedChild("A", "ValueA"),
                new NestedChild("B", "ValueB"),
                new NestedChild("C", "ValueC"),
                new NestedChild("D", "ValueD"),
                new NestedChild("E", "ValueE"),
                new NestedChild("F", "ValueF")
            };

            var parents = new[]
            {
                new ClassWithNestedObservableCollection(1, new[] { children[0], children[1] }),
                new ClassWithNestedObservableCollection(2, new[] { children[2], children[3] }),
                new ClassWithNestedObservableCollection(3, new[] { children[4] })
            };

            _source.AddRange(parents);

            _results.Count.Should().Be(5);
            _results.Items.Should().BeEquivalentTo(parents.SelectMany(p => p.Children.Take(5).Select(c => new ProjectedNestedChild(p, c))));
        }

        [Fact]
        public void RemoveParent()
        {
            var children = new[]
            {
                new NestedChild("A", "ValueA"),
                new NestedChild("B", "ValueB"),
                new NestedChild("C", "ValueC"),
                new NestedChild("D", "ValueD"),
                new NestedChild("E", "ValueE"),
                new NestedChild("F", "ValueF")
            };

            var parents = new[]
            {
                new ClassWithNestedObservableCollection(1, new[] { children[0], children[1] }),
                new ClassWithNestedObservableCollection(2, new[] { children[2], children[3] }),
                new ClassWithNestedObservableCollection(3, new[] { children[4] })
            };

            _source.AddRange(parents);

            //remove a parent and check children have moved
            _source.Remove(parents[0]);
            _results.Count.Should().Be(3);
            _results.Items.Should().BeEquivalentTo(parents.Skip(1).SelectMany(p => p.Children.Select(c => new ProjectedNestedChild(p, c))));
        }

        [Fact]
        public void RemoveChild()
        {
            var children = new[]
            {
                new NestedChild("A", "ValueA"),
                new NestedChild("B", "ValueB"),
                new NestedChild("C", "ValueC"),
                new NestedChild("D", "ValueD"),
                new NestedChild("E", "ValueE"),
                new NestedChild("F", "ValueF")
            };

            var parents = new[]
            {
                new ClassWithNestedObservableCollection(1, new[] { children[0], children[1] }),
                new ClassWithNestedObservableCollection(2, new[] { children[2], children[3] }),
                new ClassWithNestedObservableCollection(3, new[] { children[4] })
            };

            _source.AddRange(parents);

            //remove a child
            parents[1].Children.Remove(children[3]);
            _results.Count.Should().Be(4);
            _results.Items.Should().BeEquivalentTo(parents.SelectMany(p => p.Children.Where(child => child.Name != "D").Select(c => new ProjectedNestedChild(p, c))));
        }

        private class ProjectedNestedChild
        {
            public ClassWithNestedObservableCollection Parent { get; }

            public NestedChild Child { get; }

            public ProjectedNestedChild(ClassWithNestedObservableCollection parent, NestedChild child)
            {
                Parent = parent;
                Child = child;
            }
        }

        private class ProjectNestedChildEqualityComparer : IEqualityComparer<ProjectedNestedChild>
        {
            public bool Equals(ProjectedNestedChild x, ProjectedNestedChild y)
            {
                if (x == null || y == null)
                    return false;

                return x.Child.Name == y.Child.Name;
            }

            public int GetHashCode(ProjectedNestedChild obj)
            {
                return obj.Child.Name.GetHashCode();
            }
        }

        private class NestedChild
        {
            public string Name { get; }
            public string Value { get; }

            public NestedChild(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }

        private class ClassWithNestedObservableCollection
        {
            public int Id { get; }
            public ObservableCollection<NestedChild> Children { get; }

            public ClassWithNestedObservableCollection(int id, IEnumerable<NestedChild> animals)
            {
                Id = id;
                Children = new ObservableCollection<NestedChild>(animals);
            }
        }
    }
}
