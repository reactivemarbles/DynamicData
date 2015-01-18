using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Operators;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class TransformSafeParallelFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<PersonWithGender, string> _results;
        private IList<Error<Person, string>> _errors;

        private readonly Func<Person, PersonWithGender> _transformFactory = p =>
                                                                                {
                                                                                    if (p.Age % 3 == 0)
                                                                                    {
                                                                                        throw new Exception(string.Format("Cannot transform {0}", p));
                                                                                    }
                                                                                    string gender = p.Age % 2 == 0 ? "M" : "F";
                                                                                    return new PersonWithGender(p, gender);
                                                                                };

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p=>p.Name);
            _errors = new List<Error<Person, string>>();

            var safeTransform = _source.Connect().TransformSafe(_transformFactory, error => _errors.Add(error));
            _results = new ChangeSetAggregator<PersonWithGender, string>(safeTransform);
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void AddWithNoError()
        {
            var person = new Person("Adult1", 50);
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));

            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 updates");
            Assert.AreEqual(1, _results.Data.Count, "Should be 1 item in the cache");
            Assert.AreEqual(_transformFactory(person), _results.Data.Items.First(), "Should be same person");
        }

        [Test]
        public void AddWithError()
        {
            var person = new Person("Person", 3);
            _source.BatchUpdate(updater => updater.AddOrUpdate(person));

            Assert.AreEqual(1, _errors.Count, "Should be 1 error reported");
            Assert.AreEqual(0, _results.Messages.Count, "Should be no messages");
        }

        [Test]
        public void UpdateSucessively()
        {
            const string key = "Adult1";
            var update1 = new Person(key, 1);
            var update2 = new Person(key, 2);
            var update3 = new Person(key, 3);

            _source.BatchUpdate(updater => updater.AddOrUpdate(update1));
            _source.BatchUpdate(updater => updater.AddOrUpdate(update2));
            _source.BatchUpdate(updater => updater.AddOrUpdate(update3));

            Assert.AreEqual(1, _errors.Count, "Should be 1 error reported");
            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 messages");

            Assert.AreEqual(1, _results.Data.Count, "Should 1 item in the cache");
            Assert.AreEqual(_transformFactory(update2), _results.Data.Items.First(), "Change 2 shoud be the only item cached");
        }

        [Test]
        public void UpdateBatch()
        {
            const string key = "Adult1";
            var update1 = new Person(key, 1);
            var update2 = new Person(key, 2);
            var update3 = new Person(key, 3);

            _source.BatchUpdate(updater =>
                               {
                                   updater.AddOrUpdate(update1);
                                   updater.AddOrUpdate(update2);
                                   updater.AddOrUpdate(update3);
                               });

            Assert.AreEqual(1, _errors.Count, "Should be 1 error reported");
            Assert.AreEqual(1, _results.Messages.Count, "Should be 1 messages");

            Assert.AreEqual(1, _results.Data.Count, "Should 1 item in the cache");
            Assert.AreEqual(_transformFactory(update2), _results.Data.Items.First(), "Change 2 shoud be the only item cached");

        }


        [Test]
        public void UpdateBatchAndClear()
        {
            var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

            _source.BatchUpdate(updater => updater.AddOrUpdate(people));
            _source.BatchUpdate(updater => updater.Clear());

            Assert.AreEqual(2, _results.Messages.Count, "Should be 2 updates");

            Assert.AreEqual(33, _errors.Count, "Should be 33 errors");
            Assert.AreEqual(67, _results.Messages[0].Adds, "Should be 67 add");
            Assert.AreEqual(67, _results.Messages[1].Removes, "Should be 67 removes");
            Assert.AreEqual(0, _results.Data.Count, "Should be nothing cached");

        }


    }
}