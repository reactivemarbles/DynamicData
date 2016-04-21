using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class TransformFixture
    {
        [Test]
        public void ReTransformAll()
        {
            var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
            var forceTransform = new Subject<Unit>();

            using (var stub = new TransformStub(forceTransform))
            {
                stub.Source.AddOrUpdate(people);
                forceTransform.OnNext(Unit.Default);

                Assert.AreEqual(2, stub.Results.Messages.Count);
                Assert.AreEqual(10, stub.Results.Messages[1].Updates);

                for (int i = 1; i <= 10; i++)
                {
                    var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
                    var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;

                    Assert.AreEqual(original, updated);
                    Assert.IsFalse(ReferenceEquals(original, updated));
                }
            }
        }

        [Test]
        public void ReTransformSelected()
        {
            var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
            var forceTransform = new Subject<Func<Person, bool>>();

            using (var stub = new TransformStub(forceTransform))
            {
                stub.Source.AddOrUpdate(people);
                forceTransform.OnNext(person => person.Age <= 5);

                Assert.AreEqual(2, stub.Results.Messages.Count);
                Assert.AreEqual(5, stub.Results.Messages[1].Updates);

                for (int i = 1; i <= 5; i++)
                {
                    var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
                    var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
                    Assert.AreEqual(original, updated);
                    Assert.IsFalse(ReferenceEquals(original, updated));
                }
            }
        }

        [Test]
        public void Add()
        {
            using (var stub = new TransformStub())
            {
                var person = new Person("Adult1", 50);
                stub.Source.AddOrUpdate(person);

                Assert.AreEqual(1, stub.Results.Messages.Count, "Should be 1 updates");
                Assert.AreEqual(1, stub.Results.Data.Count, "Should be 1 item in the cache");
                Assert.AreEqual(stub.TransformFactory(person), stub.Results.Data.Items.First(), "Should be same person");
            }
        }

        [Test]
        public void Remove()
        {
            const string key = "Adult1";
            var person = new Person(key, 50);

            using (var stub = new TransformStub())
            {
                stub.Source.AddOrUpdate(person);
                stub.Source.Remove(key);

                Assert.AreEqual(2, stub.Results.Messages.Count, "Should be 2 updates");
                Assert.AreEqual(2, stub.Results.Messages.Count, "Should be 2 updates");
                Assert.AreEqual(1, stub.Results.Messages[0].Adds, "Should be 80 addes");
                Assert.AreEqual(1, stub.Results.Messages[1].Removes, "Should be 80 removes");
                Assert.AreEqual(0, stub.Results.Data.Count, "Should be nothing cached");
            }
        }

        [Test]
        public void Update()
        {
            const string key = "Adult1";
            var newperson = new Person(key, 50);
            var updated = new Person(key, 51);

            using (var stub = new TransformStub())
            {
                stub.Source.AddOrUpdate(newperson);
                stub.Source.AddOrUpdate(updated);

                Assert.AreEqual(2, stub.Results.Messages.Count, "Should be 2 updates");
                Assert.AreEqual(1, stub.Results.Messages[0].Adds, "Should be 1 adds");
                Assert.AreEqual(1, stub.Results.Messages[1].Updates, "Should be 1 update");
            }
        }

        [Test]
        public void BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new TransformStub())
            {
                stub.Source.AddOrUpdate(people);

                Assert.AreEqual(1, stub.Results.Messages.Count, "Should be 1 updates");
                Assert.AreEqual(100, stub.Results.Messages[0].Adds, "Should return 100 adds");

                var transformed = people.Select(stub.TransformFactory).OrderBy(p => p.Age).ToArray();
                CollectionAssert.AreEqual(transformed, stub.Results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
            }
        }

        [Test]
        public void SameKeyChanges()
        {
            using (var stub = new TransformStub())
            {
                var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

                stub.Source.AddOrUpdate(people);

                Assert.AreEqual(1, stub.Results.Messages.Count, "Should be 1 updates");
                Assert.AreEqual(1, stub.Results.Messages[0].Adds, "Should return 1 adds");
                Assert.AreEqual(9, stub.Results.Messages[0].Updates, "Should return 9 adds");
                Assert.AreEqual(1, stub.Results.Data.Count, "Should result in 1 record");

                var lastTransformed = stub.TransformFactory(people.Last());
                var onlyItemInCache = stub.Results.Data.Items.First();

                Assert.AreEqual(lastTransformed, onlyItemInCache, "Incorrect transform result");
            }
        }

        [Test]
        public void Clear()
        {
            using (var stub = new TransformStub())
            {
                var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

                stub.Source.AddOrUpdate(people);
                stub.Source.Clear();

                Assert.AreEqual(2, stub.Results.Messages.Count, "Should be 2 updates");
                Assert.AreEqual(100, stub.Results.Messages[0].Adds, "Should be 80 addes");
                Assert.AreEqual(100, stub.Results.Messages[1].Removes, "Should be 80 removes");
                Assert.AreEqual(0, stub.Results.Data.Count, "Should be nothing cached");
            }
        }

        private class TransformStub : IDisposable
        {
            public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);
            public ChangeSetAggregator<PersonWithGender, string> Results { get; }

            public Func<Person, PersonWithGender> TransformFactory { get; } = p => new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");

            public TransformStub()
            {
                Results = new ChangeSetAggregator<PersonWithGender, string>
                    (
                    Source.Connect().Transform(TransformFactory)
                    );
            }

            public TransformStub(IObservable<Unit> retransformer)
            {
                Results = new ChangeSetAggregator<PersonWithGender, string>
                    (
                    Source.Connect().Transform(TransformFactory, retransformer)
                    );
            }

            public TransformStub(IObservable<Func<Person, bool>> retransformer)
            {
                Results = new ChangeSetAggregator<PersonWithGender, string>
                    (
                    Source.Connect().Transform(TransformFactory, retransformer)
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
