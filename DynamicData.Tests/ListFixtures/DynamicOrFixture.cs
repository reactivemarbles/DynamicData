using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class DynamicOrFixture
    {
        private SourceList<IObservableList<int>> _source;

        [SetUp]
        public void Initialize()
        {
            _source = new SourceList<IObservableList<int>>();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
        }


        [Test]
        public void DynamicConcatOrderPreservingShouldWork()
        {
            var a = new SourceList<int>();
            var b = new SourceList<int>();
            var c = new SourceList<int>();
            var target = _source.DynamicConcatOrderPreserving().Connect().AsAggregator();

            CollectionAssert.AreEqual(target.Data.Items, new int [] {  });

            a.Add(1);

            b.Add(3);

            c.Add(1);
            c.Add(5);

            CollectionAssert.AreEqual( new int [] {  },target.Data.Items);

            _source.Add(a);
            CollectionAssert.AreEqual( new [] { 1 },target.Data.Items);
            _source.Add(b);
            CollectionAssert.AreEqual( new [] { 1,3 },target.Data.Items);
            _source.Add(c);
            // It's a list not a set so duplicates should be in order.
            CollectionAssert.AreEqual( new [] { 1,3,1,5 },target.Data.Items);
            _source.Remove(c);
            CollectionAssert.AreEqual( new [] { 1,3 },target.Data.Items);
            _source.Remove(a);
            CollectionAssert.AreEqual( new [] { 3 },target.Data.Items);
            a.Add(7);
            CollectionAssert.AreEqual( new [] { 3 },target.Data.Items);
            b.Add(7);
            CollectionAssert.AreEqual( new [] { 3,7 },target.Data.Items);
            b.Add(8);
            CollectionAssert.AreEqual( new [] { 3,7,8 },target.Data.Items);
            _source.Remove(b);
            CollectionAssert.AreEqual( new int [] { },target.Data.Items);
        }

        [Test]
        public void DynamicConcatNonOrderPreservingShouldWork()
        {
            var a = new SourceList<int>();
            var b = new SourceList<int>();
            var c = new SourceList<int>();
            var target = _source.DynamicConcatNonOrderPreserving().Connect().AsAggregator();

            CollectionAssert.AreEqual(target.Data.Items, new int [] {  });

            a.Add(1);

            b.Add(3);

            c.Add(1);
            c.Add(5);

            CollectionAssert.AreEquivalent( new int [] {  },target.Data.Items);

            _source.Add(a);
            CollectionAssert.AreEquivalent( new [] { 1 },target.Data.Items);
            _source.Add(b);
            CollectionAssert.AreEquivalent( new [] { 1,3 },target.Data.Items);
            _source.Add(c);
            // It's a list not a set so duplicates should be in order.
            Assert.AreEqual(target.Data.Items.Count(), 4);
            CollectionAssert.AreEquivalent( new [] { 1,3,1,5 },target.Data.Items);
            _source.Remove(c);
            CollectionAssert.AreEquivalent( new [] { 1,3 },target.Data.Items);
            _source.Remove(a);
            CollectionAssert.AreEquivalent( new [] { 3 },target.Data.Items);
            a.Add(7);
            CollectionAssert.AreEquivalent( new [] { 3 },target.Data.Items);
            b.Add(7);
            CollectionAssert.AreEquivalent( new [] { 3,7 },target.Data.Items);
            b.Add(8);
            CollectionAssert.AreEquivalent( new [] { 3,7,8 },target.Data.Items);
            _source.Remove(b);
            CollectionAssert.AreEquivalent( new int [] { },target.Data.Items);



        }


    }
}
