using System;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    public class UpdateFixture
    {
        [Test]
        public void Add()
        {
            var person = new Person("Person", 10);
            var update = new Change<Person, string>(ChangeReason.Add, "Person", person);

            update.Key.Should().Be("Person");
            update.Reason.Should().Be(ChangeReason.Add);
            update.Current.Should().Be(person);
            update.Previous.Should().Be(Optional.None<Person>());
        }

        [Test]
        public void Remove()
        {
            var person = new Person("Person", 10);
            var update = new Change<Person, string>(ChangeReason.Remove, "Person", person);

            update.Key.Should().Be("Person");
            update.Reason.Should().Be(ChangeReason.Remove);
            update.Current.Should().Be(person);
            update.Previous.Should().Be(Optional.None<Person>());
        }

        [Test]
        public void Update()
        {
            var current = new Person("Person", 10);
            var previous = new Person("Person", 9);
            var update = new Change<Person, string>(ChangeReason.Update, "Person", current, previous);

            update.Key.Should().Be("Person");
            update.Reason.Should().Be(ChangeReason.Update);
            update.Current.Should().Be(current);
            update.Previous.HasValue.Should().BeTrue();
            update.Previous.Value.Should().Be(previous);
        }

        [Test]
        public void UpdateWillThrowIfNoPreviousValueIsSupplied()
        {
            var current = new Person("Person", 10);
            Assert.Throws<ArgumentException>(() => new Change<Person, string>(ChangeReason.Update, "Person", current));
        }
    }
}
