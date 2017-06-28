using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class RemoveManyFixture
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
            _list.AddRange(Enumerable.Range(1, 10));
            _list.RemoveMany(Enumerable.Range(2, 8));
            _list.ShouldAllBeEquivalentTo(new[] {1, 10});
        }

        [Test]
        public void DoesNotRemoveDuplicates()
        {
            _list.AddRange(new[] {1, 1, 1, 5, 6, 7});
            _list.RemoveMany(new[] {1, 1, 7});
            _list.ShouldAllBeEquivalentTo(new[] {1, 5, 6});
        }

        [Test]
        public void RemoveLargeBatch()
        {
            var toAdd = Enumerable.Range(1, 10000).ToArray();
            _list.AddRange(toAdd);

            var toRemove = _list.Take(_list.Count / 2).OrderBy(x => Guid.NewGuid()).ToArray();
            _list.RemoveMany(toRemove);
            _list.ShouldAllBeEquivalentTo(toAdd.Except(toRemove));
        }
    }
}
