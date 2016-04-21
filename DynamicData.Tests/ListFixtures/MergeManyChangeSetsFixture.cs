using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class MergeManyChangeSetsFixture
    {
        [Test]
        public void MergeManyShouldWork()
        {
            var a = new SourceList<int>();
            var b = new SourceList<int>();
            var c = new SourceList<int>();

            var parent = new SourceList<SourceList<int>>();
            parent.Add(a);
            parent.Add(b);
            parent.Add(c);

            var d = parent.Connect()
                          .MergeMany(e => e.Connect().RemoveIndex())
                          .AsObservableList();

            Assert.AreEqual(d.Count, 0);

            a.Add(1);

            Assert.AreEqual(d.Count, 1);
            a.Add(2);
            Assert.AreEqual(d.Count, 2);

            b.Add(3);
            Assert.AreEqual(d.Count, 3);
            b.Add(5);
            Assert.AreEqual(d.Count, 4);
            CollectionAssert.AreEquivalent(d.Items, new[] { 1, 2, 3, 5 });

            b.Clear();

            // Fails below
            Assert.AreEqual(d.Count, 2);
            CollectionAssert.AreEquivalent(d.Items, new[] { 1, 2 });
        }
    }
}
