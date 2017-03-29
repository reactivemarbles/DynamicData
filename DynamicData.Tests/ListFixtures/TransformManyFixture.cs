using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class TransformManyFixture
    {
        private ISourceList<PersonWithRelations> _source;
        private ChangeSetAggregator<PersonWithRelations> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<PersonWithRelations>();

            _results = _source.Connect()
                .TransformMany(p => p.Relations.RecursiveSelect(r => r.Relations))
                .AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void Add()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] {frientofchild1});
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] {child1, child2, child3});
            //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

            _source.Add(mother);

            _results.Data.Count.Should().Be(4);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] { child1, child2, child3, frientofchild1 });
        }

        [Test]
        public void RemoveParent()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] {frientofchild1});
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] {child1, child2, child3});


            _source.Add(mother);
            _source.Remove(mother);
            _results.Data.Count.Should().Be(0);

        }

        [Test]
        public void Replace()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] {frientofchild1});
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] {child1, child2, child3});

            _source.Add(mother);

            var child4 = new PersonWithRelations("Child4", 2);
            var updatedMother = new PersonWithRelations("Mother", 35, new[] {child1, child2, child4});

            _source.Replace(mother, updatedMother);

            _results.Data.Count.Should().Be(4);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] { child1, child2, frientofchild1, child4 });
        }

        [Test]
        public void AddRange()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });

            _source.Add(mother);

            var child4 = new PersonWithRelations("Child4", 1);
            var child5 = new PersonWithRelations("Child5", 2);
            var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });


            var child6 = new PersonWithRelations("Child6", 1);
            var child7 = new PersonWithRelations("Child7", 2);
            var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

            _source.AddRange(new[] {anotherRelative1, anotherRelative2});
            _results.Data.Count.Should().Be(8);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] { child1, child2, child3, frientofchild1, child4, child5, child6, child7 });

        }

        [Test]
        public void RemoveRange()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
            var child4 = new PersonWithRelations("Child4", 1);
            var child5 = new PersonWithRelations("Child5", 2);
            var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });
            var child6 = new PersonWithRelations("Child6", 1);
            var child7 = new PersonWithRelations("Child7", 2);
            var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

            _source.AddRange(new[] { mother, anotherRelative1, anotherRelative2 });

            _source.RemoveRange(0,2);
            _results.Data.Count.Should().Be(2);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] { child6, child7 });

        }


        [Test]
        public void Clear()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
            var child4 = new PersonWithRelations("Child4", 1);
            var child5 = new PersonWithRelations("Child5", 2);
            var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });
            var child6 = new PersonWithRelations("Child6", 1);
            var child7 = new PersonWithRelations("Child7", 2);
            var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

            _source.AddRange(new[] { mother, anotherRelative1, anotherRelative2 });

            _source.Clear();
            _results.Data.Count.Should().Be(0);

        }

        [Test]
        public void Move()
        {
            //Move should have no effect 

            var child4 = new PersonWithRelations("Child4", 1);
            var child5 = new PersonWithRelations("Child5", 2);
            var anotherRelative1 = new PersonWithRelations("Another1", 2, new[] { child4, child5 });
            var child6 = new PersonWithRelations("Child6", 1);
            var child7 = new PersonWithRelations("Child7", 2);
            var anotherRelative2 = new PersonWithRelations("Another2", 2, new[] { child6, child7 });

            _source.AddRange(new[] { anotherRelative1, anotherRelative2 });

            _results.Messages.Count.Should().Be(1);
            _source.Move(1,0);
            _results.Messages.Count.Should().Be(1);

        }
    }


}
