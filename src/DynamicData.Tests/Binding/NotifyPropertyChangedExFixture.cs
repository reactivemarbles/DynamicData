using System;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class NotifyPropertyChangedExFixture
{
    [Theory, InlineData(true), InlineData(false)]
    public void SubscribeToPropertyChangeForAllItemsInList(bool notifyOnInitialValue)
    {
        var lastChange = new PropertyValue<Person, int>(new Person(), -1);
        var source = new SourceList<Person>();
        source.Connect().WhenPropertyChanged(p => p.Age, notifyOnInitialValue).Subscribe(c => lastChange = c);
        var person = new Person("Name", 10);
        var anotherPerson = new Person("AnotherName", 10);
        source.Add(person);
        source.Add(anotherPerson);

        if (notifyOnInitialValue)
        {
            anotherPerson.Should().Be(lastChange.Sender);
            lastChange.Value.Should().Be(10);
        }
        else
        {
            lastChange.Sender.Name.Should().Be("unknown");
            lastChange.Value.Should().Be(-1);
        }

        person.Age = 12;
        lastChange.Sender.Should().Be(person);
        lastChange.Value.Should().Be(12);
        anotherPerson.Age = 13;
        lastChange.Sender.Should().Be(anotherPerson);
        lastChange.Value.Should().Be(13);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void SubscribeToProperyChangedOnASingleItem(bool notifyOnInitialValue)
    {
        var lastChange = new PropertyValue<Person, int>(new Person(), -1);
        var person = new Person("Name", 10);
        person.WhenPropertyChanged(p => p.Age, notifyOnInitialValue).Subscribe(c => lastChange = c);

        if (notifyOnInitialValue)
        {
            lastChange.Sender.Should().Be(person);
            lastChange.Value.Should().Be(10);
        }
        else
        {
            lastChange.Sender.Name.Should().Be("unknown");
            lastChange.Value.Should().Be(-1);
        }

        person.Age = 12;
        lastChange.Sender.Should().Be(person);
        lastChange.Value.Should().Be(12);
        person.Age = 13;
        lastChange.Sender.Should().Be(person);
        lastChange.Value.Should().Be(13);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void SubscribeToValueChangedOnASingleItem(bool notifyOnInitialValue)
    {
        var age = -1;
        var person = new Person("Name", 10);
        person.WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => age = i);

        (notifyOnInitialValue ? 10 : -1).Should().Be(age);
        person.Age = 12;
        age.Should().Be(12);
        person.Age = 13;
        age.Should().Be(13);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void SubscribeToValueChangeForAllItemsInList(bool notifyOnInitialValue)
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
        lastAgeChange.Should().Be(12);
        anotherPerson.Age = 13;
        lastAgeChange.Should().Be(13);
    }
}
