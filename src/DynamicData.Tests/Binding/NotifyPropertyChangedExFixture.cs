using System;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Binding
{
    public class NotifyPropertyChangedExFixture
    {
        [Theory,
         InlineData(true),
         InlineData(false)]
        public void SubscribeToValueChangeForAllItemsInList( bool notifyOnInitialValue)
        {
            var lastAgeChange = -1;
            var source = new SourceList<Person>();
            source.Connect().WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => lastAgeChange = i);
            var person = new Person("Name", 10);
            var anotherPerson = new Person("AnotherName", 10);
            source.Add(person);
            source.Add(anotherPerson);

            (notifyOnInitialValue ? 10 : -1).Should().Be(lastAgeChange);
            person.Age = 12;
            12.Should().Be(lastAgeChange);
            anotherPerson.Age = 13;
            13.Should().Be(lastAgeChange);
        }

        [Theory,
         InlineData(true),
         InlineData(false)]
        public void SubscribeToValueChangedOnASingleItem( bool notifyOnInitialValue)
        {
            var age = -1;
            var person = new Person("Name", 10);
            person.WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => age = i);

            (notifyOnInitialValue ? 10 : -1).Should().Be(age);
            person.Age = 12;
            12.Should().Be(age);
            person.Age = 13;
            13.Should().Be(age);
        }

        [Theory,
         InlineData(true),
         InlineData(false)]
        public void SubscribeToPropertyChangeForAllItemsInList( bool notifyOnInitialValue)
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
                anotherPerson.Should().Be(lastChange.Sender);
                10.Should().Be(lastChange.Value);
            }
            else
            {
                lastChange.Sender.Should().BeNull();
                (-1).Should().Be(lastChange.Value);
            }

            person.Age = 12;
            person.Should().Be(lastChange.Sender);
            12.Should().Be(lastChange.Value);
            anotherPerson.Age = 13;
            anotherPerson.Should().Be(lastChange.Sender);
            13.Should().Be(lastChange.Value);
        }

        [Theory,
         InlineData(true),
         InlineData(false)]
        public void SubscribeToProperyChangedOnASingleItem( bool notifyOnInitialValue)
        {
            var lastChange = new PropertyValue<Person, int>(null, -1);
            var person = new Person("Name", 10);
            person.WhenPropertyChanged(p => p.Age, notifyOnInitialValue).Subscribe(c => lastChange = c);

            if (notifyOnInitialValue)
            {
                person.Should().Be(lastChange.Sender);
                10.Should().Be(lastChange.Value);
            }
            else
            {
                lastChange.Sender.Should().BeNull();
                (-1).Should().Be(lastChange.Value);
            }
            person.Age = 12;
            person.Should().Be(lastChange.Sender);
            12.Should().Be(lastChange.Value);
            person.Age = 13;
            person.Should().Be(lastChange.Sender);
            13.Should().Be(lastChange.Value);
        }


    }
}