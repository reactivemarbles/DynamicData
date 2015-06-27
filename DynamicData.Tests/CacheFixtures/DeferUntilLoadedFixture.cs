using System;
using System.Linq;
using DynamicData.Tests.Domain;
using NUnit.Framework;


namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class DeferAnsdSkipFixture
    {
        [Test]
        public void DeferUntilLoadedDoesNothingUntilDataHasBeenReceived()
        {
            bool updateReceived = false;
            IChangeSet<Person, string> result = null;

            var cache = new SourceCache<Person, string>(p => p.Name);


            var deferStream = cache.Connect().DeferUntilLoaded()
                                .Subscribe(changes =>
                                           {
                                               updateReceived = true;
                                               result = changes;
                                           });

            Assert.IsFalse(updateReceived,"No update should be received");
            cache.AddOrUpdate(new Person("Test",1));

            Assert.IsTrue(updateReceived,"Replace should be received");
            Assert.AreEqual(1,result.Adds);
            Assert.AreEqual(new Person("Test",1), result.First().Current);
            deferStream.Dispose();
        }

        [Test]
        public void SkipInitialDoesNotReturnTheFirstBatchOfData()
        {
            bool updateReceived = false;

            var cache = new SourceCache<Person, string>(p => p.Name);


            var deferStream = cache.Connect().SkipInitial()
                                .Subscribe(changes => updateReceived = true);

            Assert.IsFalse(updateReceived, "No update should be received");

            cache.AddOrUpdate(new Person("P1", 1));

            Assert.IsFalse(updateReceived, "No update should be received for initial batch of changes");

            cache.AddOrUpdate(new Person("P2", 2));
            Assert.IsTrue(updateReceived, "Replace should be received");
            deferStream.Dispose();
        }
    }
}