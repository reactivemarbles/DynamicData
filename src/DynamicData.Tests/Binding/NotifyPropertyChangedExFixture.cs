using System.Runtime.CompilerServices;

#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Binding;

public class NotifyPropertyChangedExFixture
{
    [Test, Arguments(true), Arguments(false)]
    public async Task SubscribeToPropertyChangeForAllItemsInList(bool notifyOnInitialValue)
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
            await Assert.That(anotherPerson).IsEqualTo(lastChange.Sender);
            await Assert.That(lastChange.Value).IsEqualTo(10);
        }
        else
        {
            await Assert.That(lastChange.Sender.Name).IsEqualTo("unknown");
            await Assert.That(lastChange.Value).IsEqualTo(-1);
        }

        person.Age = 12;
        await Assert.That(lastChange.Sender).IsEqualTo(person);
        await Assert.That(lastChange.Value).IsEqualTo(12);
        anotherPerson.Age = 13;
        await Assert.That(lastChange.Sender).IsEqualTo(anotherPerson);
        await Assert.That(lastChange.Value).IsEqualTo(13);
    }

    [Test, Arguments(true), Arguments(false)]
    public async Task SubscribeToProperyChangedOnASingleItem(bool notifyOnInitialValue)
    {
        var lastChange = new PropertyValue<Person, int>(new Person(), -1);
        var person = new Person("Name", 10);
        person.WhenPropertyChanged(p => p.Age, notifyOnInitialValue).Subscribe(c => lastChange = c);

        if (notifyOnInitialValue)
        {
            await Assert.That(lastChange.Sender).IsEqualTo(person);
            await Assert.That(lastChange.Value).IsEqualTo(10);
        }
        else
        {
            await Assert.That(lastChange.Sender.Name).IsEqualTo("unknown");
            await Assert.That(lastChange.Value).IsEqualTo(-1);
        }

        person.Age = 12;
        await Assert.That(lastChange.Sender).IsEqualTo(person);
        await Assert.That(lastChange.Value).IsEqualTo(12);
        person.Age = 13;
        await Assert.That(lastChange.Sender).IsEqualTo(person);
        await Assert.That(lastChange.Value).IsEqualTo(13);
    }

    [Test, Arguments(true), Arguments(false)]
    public async Task SubscribeToValueChangedOnASingleItem(bool notifyOnInitialValue)
    {
        var age = -1;
        var person = new Person("Name", 10);
        person.WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => age = i);

        await Assert.That((notifyOnInitialValue ? 10 : -1)).IsEqualTo(age);
        person.Age = 12;
        await Assert.That(age).IsEqualTo(12);
        person.Age = 13;
        await Assert.That(age).IsEqualTo(13);
    }

    [Test, Arguments(true), Arguments(false)]
    public async Task SubscribeToValueChangeForAllItemsInList(bool notifyOnInitialValue)
    {
        var lastAgeChange = -1;
        var source = new SourceList<Person>();
        source.Connect().WhenValueChanged(p => p.Age, notifyOnInitialValue).Subscribe(i => lastAgeChange = i);
        var person = new Person("Name", 10);
        var anotherPerson = new Person("AnotherName", 10);
        source.Add(person);
        source.Add(anotherPerson);

        await Assert.That((notifyOnInitialValue ? 10 : -1)).IsEqualTo(lastAgeChange);
        person.Age = 12;
        await Assert.That(lastAgeChange).IsEqualTo(12);
        anotherPerson.Age = 13;
        await Assert.That(lastAgeChange).IsEqualTo(13);
    }

    [Test]
    public async Task CastToNullable()
    {
        var parent = new TestEntity()
        {
            Id = 1,
            Age = 10
        };

        using var subscription = parent.WhenValueChanged(
                propertyAccessor: static entity => (int?)entity.Child.Age,
                notifyOnInitialValue: true,
                fallbackValue: static () => null)
            .RecordValues(out var results);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.HasCompleted).IsFalse();
        await Assert.That(results.RecordedValues).HasSingleItem();
        await Assert.That(results.RecordedValues[0]).IsNull().Because("the target entity has no child");

        var child = new TestEntity()
        {
            Id = 2,
            Age = 5
        };
        parent.Child = child;

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.HasCompleted).IsFalse();
        await Assert.That(results.RecordedValues.Skip(1)).HasSingleItem();
        await Assert.That(results.RecordedValues.Skip(1).First()).IsEqualTo(child.Age).Because("a child of age 5 was added");

        child.Age = 6;

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.HasCompleted).IsFalse();
        await Assert.That(results.RecordedValues.Skip(2)).HasSingleItem();
        await Assert.That(results.RecordedValues.Skip(2).First()).IsEqualTo(child.Age).Because("the child entity's age was changed");
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
            ref T field,
                                T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
