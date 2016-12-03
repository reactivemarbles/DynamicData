using System;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{
    public class NotifyPropertyChangedExFixture
    {
        [Test]
        public void SubscribeToValueChangeForAllItemsInList([Values(true, false)] bool notifyOnInitialValue)
        {
            var lastAgeChange = -1;
            var source = new SourceList<Person>();
            source.Connect().WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => lastAgeChange = i);
            var person = new Person("Name", 10);
            var anotherPerson = new Person("AnotherName", 10);
            source.Add(person);
            source.Add(anotherPerson);

            Assert.That(lastAgeChange, Is.EqualTo(notifyOnInitialValue ? 10 : -1));
            person.Age = 12;
            Assert.That(lastAgeChange, Is.EqualTo(12));
            anotherPerson.Age = 13;
            Assert.That(lastAgeChange, Is.EqualTo(13));
        }

        [Test]
        public void SubscribeToValueChangedOnASingleItem([Values(true, false)] bool notifyOnInitialValue)
        {
            var age = -1;
            var person = new Person("Name", 10);
            person.WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => age = i);

            Assert.That(age, Is.EqualTo(notifyOnInitialValue ? 10 : -1));
            person.Age = 12;
            Assert.That(age, Is.EqualTo(12));
            person.Age = 13;
            Assert.That(age, Is.EqualTo(13));
        }

        [Test]
        public void SubscribeToPropertyChangeForAllItemsInList([Values(true, false)] bool notifyOnInitialValue)
        {
            var lastChange = new PropertyValue<Person, int>(null, -1);
            var source = new SourceList<Person>();
            source.Connect().WhenPropertyChanged(p => p.Age, notifyOnInitialValue).Subscribe(c => lastChange = c);
            var person = new Person("Name", 10);
            var anotherPerson = new Person("AnotherName", 10);
            source.Add(person);
            source.Add(anotherPerson);

            if (notifyOnInitialValue)
            {
                Assert.That(lastChange.Sender, Is.EqualTo(anotherPerson));
                Assert.That(lastChange.Value, Is.EqualTo(10));
            }
            else
            {
                Assert.That(lastChange.Sender, Is.Null);
                Assert.That(lastChange.Value, Is.EqualTo(-1));
            }

            person.Age = 12;
            Assert.That(lastChange.Sender, Is.EqualTo(person));
            Assert.That(lastChange.Value, Is.EqualTo(12));
            anotherPerson.Age = 13;
            Assert.That(lastChange.Sender, Is.EqualTo(anotherPerson));
            Assert.That(lastChange.Value, Is.EqualTo(13));
        }

        [Test]
        public void SubscribeToProperyChangedOnASingleItem([Values(true, false)] bool notifyOnInitialValue)
        {
            var lastChange = new PropertyValue<Person, int>(null, -1);
            var person = new Person("Name", 10);
            person.WhenPropertyChanged(p => p.Age, notifyOnInitialValue).Subscribe(c => lastChange = c);

            if (notifyOnInitialValue)
            {
                Assert.That(lastChange.Sender, Is.EqualTo(person));
                Assert.That(lastChange.Value, Is.EqualTo(10));
            }
            else
            {
                Assert.That(lastChange.Sender, Is.Null);
                Assert.That(lastChange.Value, Is.EqualTo(-1));
            }
            person.Age = 12;
            Assert.That(lastChange.Sender, Is.EqualTo(person));
            Assert.That(lastChange.Value, Is.EqualTo(12));
            person.Age = 13;
            Assert.That(lastChange.Sender, Is.EqualTo(person));
            Assert.That(lastChange.Value, Is.EqualTo(13));
        }
    }
}