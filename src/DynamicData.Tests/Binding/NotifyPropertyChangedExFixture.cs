using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using DynamicData.Binding;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

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
    
    [Fact]
    public void CastToNullable()
    {
        var parent = new TestEntity()
        {
            Id = 1,
            Age = 10
        };
        
        using var subscription = parent.WhenValueChanged(
                propertyAccessor:       static entity => (int?)entity.Child.Age,
                notifyOnInitialValue:   true,
                fallbackValue:          static () => null)
            .RecordValues(out var results);
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("additional changes could be made");
        results.RecordedValues.Should().ContainSingle("an initial value should have been published");
        results.RecordedValues[0].Should().Be(null, "the target entity has no child");
        
        var child = new TestEntity()
        {
            Id = 2,
            Age = 5
        };
        parent.Child = child;
        
        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("additional changes could be made");
        results.RecordedValues.Skip(1).Should().ContainSingle("a single change was performed");
        results.RecordedValues.Skip(1).First().Should().Be(child.Age, "a child of age 5 was added");
        
        child.Age = 6;

        results.Error.Should().BeNull("no errors should have occurred");
        results.HasCompleted.Should().BeFalse("additional changes could be made");
        results.RecordedValues.Skip(2).Should().ContainSingle("a single change was performed");
        results.RecordedValues.Skip(2).First().Should().Be(child.Age, "the child entity's age was changed");
    }
    
    public class TestEntity
        : INotifyPropertyChanged
    {
        public long Id { get; init; }
        
        public int Age
        {
            get;
            set => SetPropertyField(ref field, value);
        }
        
        public TestEntity? Child
        {
            get;
            set => SetPropertyField(ref field, value);
        } 
            
        public event PropertyChangedEventHandler? PropertyChanged;
            
        protected void SetPropertyField<T>(
            ref                 T       field,
                                T       value,
            [CallerMemberName]  string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            
            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
