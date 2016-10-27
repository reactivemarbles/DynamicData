using System;
using System.Linq;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class FilterOnPropertyFixture
    {
        [Test]
        public void InitialValues()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddOrUpdate(people);

                Assert.That(stub.Results.Messages.Count, Is.EqualTo(1));
                Assert.That(stub.Results.Data.Count, Is.EqualTo(82));

                CollectionAssert.AreEquivalent(people.Skip(18), stub.Results.Data.Items);
            }
        }

        [Test]
        public void ChangeAValueToMatchFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddOrUpdate(people);

                people[20].Age = 10;

                Assert.That(stub.Results.Messages.Count, Is.EqualTo(2));
                Assert.That(stub.Results.Data.Count, Is.EqualTo(81));
            }
        }

        [Test]
        public void ChangeAValueToNoLongerMatchFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddOrUpdate(people);

                people[10].Age = 20;

                Assert.That(stub.Results.Messages.Count, Is.EqualTo(2));
                Assert.That(stub.Results.Data.Count, Is.EqualTo(83));
            }
        }
        [Test]
        public void ChangeAValueSoItIsStillInTheFilter()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new FilterPropertyStub())
            {
                stub.Source.AddOrUpdate(people);

                people[50].Age = 100;

                Assert.That(stub.Results.Messages.Count, Is.EqualTo(1));
                Assert.That(stub.Results.Data.Count, Is.EqualTo(82));
            }
        }

        private class FilterPropertyStub : IDisposable
        {
            public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);
            public ChangeSetAggregator<Person, string> Results { get; }


            public FilterPropertyStub()
            {
                Results = new ChangeSetAggregator<Person, string>
                    (
                        Source.Connect().FilterOnProperty(p => p.Age, p => p.Age > 18)
                    );
            }

            public void Dispose()
            {
                Source.Dispose();
                Results.Dispose();
            }
        }
    }
}