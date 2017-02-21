using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class TransformSafeAsyncFixture
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
        public async Task Add()
        {
            using (var stub = new TransformStub())
            {
                var person = new Person("Adult1", 50);
                stub.Source.AddOrUpdate(person);

                Assert.AreEqual(1, stub.Results.Messages.Count, "Should be 1 updates");
                Assert.AreEqual(1, stub.Results.Data.Count, "Should be 1 item in the cache");

                var firstPerson = await stub.TransformFactory(person);

                Assert.AreEqual(firstPerson, stub.Results.Data.Items.First(), "Should be same person");
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
        public async Task BatchOfUniqueUpdates()
        {
            var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
            using (var stub = new TransformStub())
            {
                stub.Source.AddOrUpdate(people);

                Assert.AreEqual(1, stub.Results.Messages.Count, "Should be 1 updates");
                Assert.AreEqual(100, stub.Results.Messages[0].Adds, "Should return 100 adds");

                var result = await Task.WhenAll(people.Select(stub.TransformFactory));
                var transformed = result.OrderBy(p => p.Age).ToArray();
                CollectionAssert.AreEqual(transformed, stub.Results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
            }
        }

        [Test]
        public async Task SameKeyChanges()
        {
            using (var stub = new TransformStub())
            {
                var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

                stub.Source.AddOrUpdate(people);

                Assert.AreEqual(1, stub.Results.Messages.Count, "Should be 1 updates");
                Assert.AreEqual(1, stub.Results.Messages[0].Adds, "Should return 1 adds");
                Assert.AreEqual(9, stub.Results.Messages[0].Updates, "Should return 9 adds");
                Assert.AreEqual(1, stub.Results.Data.Count, "Should result in 1 record");

                var lastTransformed = await stub.TransformFactory(people.Last());
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


        [Test]
        public void HandleError()
        {
            using (var stub = new TransformStub(p =>
            {
                if (p.Age <= 50)
                    return new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");

                throw new Exception("Broken");
            }))
            {
                var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();
                stub.Source.AddOrUpdate(people);

                Assert.IsNull(stub.Results.Error);

                Exception error = null;
                stub.Source.Connect()
                    .Subscribe(changes => { }, ex => error = ex);


                Assert.IsNull(error);

                Assert.AreEqual(50, stub.HandledErrors.Count);
                Assert.AreEqual(50, stub.Results.Data.Count);
            }
        }

        private class TransformStub : IDisposable
        {
            public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);
            public ChangeSetAggregator<PersonWithGender, string> Results { get; }

            public Func< Person, Task<PersonWithGender>> TransformFactory { get; }

            public IList<Error<Person, string>> HandledErrors { get; } = new List<Error<Person, string>>(); 

            public TransformStub()
            {
                TransformFactory = (p) =>
                {
                    var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                    return Task.FromResult(result);
                };

                Results = new ChangeSetAggregator<PersonWithGender, string>
                (   
                    Source.Connect().TransformSafeAsync(TransformFactory, ErrorHandler)
                );
            }


            public TransformStub(Func<Person, PersonWithGender> factory)
            {
                TransformFactory = (p) =>
                {
                    var result = factory(p);
                    return Task.FromResult(result);
                };

                Results = new ChangeSetAggregator<PersonWithGender, string>
                (
                    Source.Connect().TransformSafeAsync(TransformFactory, ErrorHandler)
                );
            }

            public TransformStub(IObservable<Unit> retransformer)
            {
                TransformFactory = (p) =>
                {
                    var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                    return Task.FromResult(result);
                };

                Results = new ChangeSetAggregator<PersonWithGender, string>
                (
                    Source.Connect().TransformSafeAsync(TransformFactory, ErrorHandler, retransformer.Select(x =>
                    {
                        Func<Person, string, bool> transformer = (p, key) => true;
                        return transformer;
                    }))
                );
            }

            public TransformStub(IObservable<Func<Person, bool>> retransformer)
            {
                TransformFactory = (p) =>
                {
                    var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                    return Task.FromResult(result);
                };

                Results = new ChangeSetAggregator<PersonWithGender, string>
                (
                    Source.Connect().TransformSafeAsync(TransformFactory, ErrorHandler, retransformer.Select(selector =>
                    {
                        Func<Person, string, bool> transformed = (p, key) => selector(p);
                        return transformed;
                    }))
                );
            }

            private void ErrorHandler(Error<Person, string> error)
            {
                HandledErrors.Add(error);
            }

            public void Dispose()
            {
                Source.Dispose();
                Results.Dispose();
            }
        }
    }
}