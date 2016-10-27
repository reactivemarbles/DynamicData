using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
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

            _results = _source.Connect().TransformMany(p => p.Relations.RecursiveSelect(r => r.Relations))
                              .AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
        }

        [Test]
        public void RecursiveChildrenCanBeAdded()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
            //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

            _source.Add(mother);

            Assert.AreEqual(4, _results.Data.Count, "Should be 4 in the cache");
            Assert.IsTrue(_results.Data.Items.FindItemAndIndex(child1).HasValue, "Child 1 should be in the cache");
            Assert.IsTrue(_results.Data.Items.FindItemAndIndex(child2).HasValue, "Child 2 should be in the cache");
            Assert.IsTrue(_results.Data.Items.FindItemAndIndex(child3).HasValue, "Child 3 should be in the cache");
            Assert.IsTrue(_results.Data.Items.FindItemAndIndex(frientofchild1).HasValue, "Friend 1 should be in the cache");
        }

        [Test]
        public void ChildrenAreRemovedWhenParentIsRemoved()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
            //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

            _source.Add(mother);
            _source.Remove(mother);
            Assert.AreEqual(0, _results.Data.Count, "Should be 4 in the cache");
        }
    }
}
