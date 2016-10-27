using System;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    public class DistinctUpdateFixture
    {
        [Test]
        public void Add()
        {
            var person = new Person("Person", 10);
            var update = new Change<Person, Person>(ChangeReason.Add, person, person);

            Assert.AreEqual(person, update.Key);
            Assert.AreEqual(ChangeReason.Add, update.Reason);
            Assert.AreEqual(person, update.Current);
            Assert.AreEqual(Optional.None<Person>(), update.Previous);
        }

        [Test]
        public void Remove()
        {
            var person = new Person("Person", 10);
            var update = new Change<Person, Person>(ChangeReason.Remove, person, person);

            Assert.AreEqual(person, update.Key);
            Assert.AreEqual(ChangeReason.Remove, update.Reason);
            Assert.AreEqual(person, update.Current);
            Assert.AreEqual(Optional.None<Person>(), update.Previous);
        }

        [Test]
        public void Update()
        {
            var current = new Person("Person", 10);
            var previous = new Person("Person", 9);
            var update = new Change<Person, Person>(ChangeReason.Update, current, current, previous);

            Assert.AreEqual(current, update.Key);
            Assert.AreEqual(ChangeReason.Update, update.Reason);
            Assert.AreEqual(current, update.Current);
            Assert.IsTrue(update.Previous.HasValue);
            Assert.AreEqual(previous, update.Previous.Value);
        }

        [Test]
        public void UpdateWillThrowIfNoPreviousValueIsSupplied()
        {
            var current = new Person("Person", 10);

            Assert.Throws<ArgumentException>(() => new Change<Person, Person>(ChangeReason.Update, current, current));
        }
    }
}
