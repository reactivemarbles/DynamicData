using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    class RemoveManyFixture
    {

        private List<int> _list;

        [SetUp]
        public void Setup()
        {
            _list = new List<int>();
        }

        [Test]
        public void RemoveManyWillRemoveARange()
        {
            _list.AddRange(Enumerable.Range(1,10));


            _list.RemoveMany(Enumerable.Range(2, 8));

            CollectionAssert.AreEquivalent(new [] {1,10},_list);
        }

        [Test]
        public void DoesNotRemoveDuplicates()
        {
            _list.AddRange(new[] { 1, 1, 1, 5, 6, 7 });


            _list.RemoveMany(new[] {1,1,7});

            CollectionAssert.AreEquivalent(new[] { 1, 5, 6 }, _list);
        }

    }
}