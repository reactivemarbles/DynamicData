using System;
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class DeferAnsdSkipFixture
    {
        [Test]
        public void DeferUntilLoadedDoesNothingUntilDataHasBeenReceived()
        {
            bool updateReceived = false;
            IChangeSet<Person> result = null;

            var cache = new SourceList<Person>();

            var deferStream = cache.Connect().DeferUntilLoaded()
                                   .Subscribe(changes =>
                                   {
                                       updateReceived = true;
                                       result = changes;
                                   });

            var person = new Person("Test", 1);

            Assert.IsFalse(updateReceived, "No update should be received");
            cache.Add(person);

            Assert.IsTrue(updateReceived, "Replace should be received");
            Assert.AreEqual(1, result.Adds);
            Assert.AreEqual(person, result.First().Item.Current);
            deferStream.Dispose();
        }

        [Test]
        public void SkipInitialDoesNotReturnTheFirstBatchOfData()
        {
            bool updateReceived = false;

            var cache = new SourceList<Person>();

            var deferStream = cache.Connect().SkipInitial()
                                   .Subscribe(changes => updateReceived = true);

            Assert.IsFalse(updateReceived, "No update should be received");

            cache.Add(new Person("P1", 1));

            Assert.IsFalse(updateReceived, "No update should be received for initial batch of changes");

            cache.Add(new Person("P2", 2));
            Assert.IsTrue(updateReceived, "Replace should be received");
            deferStream.Dispose();
        }
    }
}
