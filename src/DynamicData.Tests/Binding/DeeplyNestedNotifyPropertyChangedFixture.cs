using System.Diagnostics;

#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

namespace DynamicData.Tests.Binding;

public class DeeplyNestedNotifyPropertyChangedFixture
{
    [Test]
    public async Task DepthOfOne()
    {
        var instance = new ClassA { Name = "Someone" };

        var chain = instance.WhenPropertyChanged(a => a.Name, true);
        string? result = null;

        var subscription = chain.Subscribe(notification => result = notification?.Value);

        await Assert.That(result).IsEqualTo("Someone");

        instance.Name = "Else";
        await Assert.That(result).IsEqualTo("Else");

        instance.Name = null;
        await Assert.That(result).IsNull();

        instance.Name = "NotNull";
        await Assert.That(result).IsEqualTo("NotNull");
    }

    // Covers https://github.com/reactivemarbles/DynamicData/issues/671
    [Test]
    public async Task NonObservableChildWithInitialValue()
    {
        var source = new ClassC()
        {
            Child = new()
            {
                Value = 1
            }
        };

        var notifications = new List<PropertyValue<ClassC, int>>();
        var error = null as Exception;
        var isCompleted = false;

        using var subscription = source
            .WhenPropertyChanged(x => x.Child!.Value, notifyOnInitialValue: true)
            .Subscribe(
                onNext: notifications.Add,
                onError: e => error = e,
                onCompleted: () => isCompleted = true);

        await Assert.That(error).IsNull();
        await Assert.That(isCompleted).IsFalse();
        await Assert.That(notifications.Count).IsEqualTo(1).Because("a notification was requested for the initial value");
        await Assert.That(notifications[0].Value).IsEqualTo(source.Child!.Value).Because("the child object's data value should have been published");

        source.Child.Value = 2;

        await Assert.That(error).IsNull();
        await Assert.That(isCompleted).IsFalse();
        await Assert.That(notifications.Count).IsEqualTo(1).Because("the object that was changed does not publish notifications");

        source.Child = new()
        {
            Value = 3
        };

        await Assert.That(error).IsNull();
        await Assert.That(isCompleted).IsFalse();
        await Assert.That(notifications.Count).IsEqualTo(2).Because("the parent object should have published a notification for its child being changed");
        await Assert.That(notifications[1].Value).IsEqualTo(source.Child!.Value).Because("the child object's data value should have been published");
    }

    [Test]
    public async Task NonObservableChildWithoutInitialValue()
    {
        var source = new ClassC()
        {
            Child = new()
            {
                Value = 1
            }
        };

        var notifications = new List<PropertyValue<ClassC, int>>();
        var error = null as Exception;
        var isCompleted = false;

        using var subscription = source
            .WhenPropertyChanged(x => x.Child!.Value, notifyOnInitialValue: false)
            .Subscribe(
                onNext: notifications.Add,
                onError: e => error = e,
                onCompleted: () => isCompleted = true);

        await Assert.That(error).IsNull();
        await Assert.That(isCompleted).IsFalse();
        await Assert.That(notifications).IsEmpty();

        source.Child.Value = 2;

        await Assert.That(error).IsNull();
        await Assert.That(isCompleted).IsFalse();
        await Assert.That(notifications).IsEmpty();

        source.Child = new()
        {
            Value = 3
        };

        await Assert.That(error).IsNull();
        await Assert.That(isCompleted).IsFalse();
        await Assert.That(notifications.Count).IsEqualTo(1).Because("the parent object should have published a notification for its child being changed");
        await Assert.That(notifications[0].Value).IsEqualTo(source.Child!.Value).Because("the child object's data value should have been published");
    }

    [Test]
    public async Task NotifiesInitialValue_WithFallback()
    {
        var instance = new ClassA { Child = new ClassB { Age = 10 } };

        //provide a fallback so a value can always be obtained
        var chain = instance.WhenChanged(a => a!.Child!.Age, (sender, a) => a, () => -1);

        int? result = null;

        var subscription = chain.Subscribe(age => result = age);

        await Assert.That(result).IsEqualTo(10);

        instance.Child.Age = 22;
        await Assert.That(result).IsEqualTo(22);

        instance.Child = new ClassB { Age = 25 };
        await Assert.That(result).IsEqualTo(25);

        instance.Child.Age = 26;
        await Assert.That(result).IsEqualTo(26);
        instance.Child = null;
        await Assert.That(result).IsEqualTo(-1);

        instance.Child = new ClassB { Age = 21 };
        await Assert.That(result).IsEqualTo(21);
    }

    [Test]
    public async Task NotifiesInitialValueAndNullChild()
    {
        var instance = new ClassA();

        var chain = instance.WhenPropertyChanged(a => a.Child!.Age, true);
        int? result = null;

        var subscription = chain.Subscribe(notification => result = notification?.Value);
        await Assert.That(result).IsNull();
        instance.Child = new ClassB { Age = 10 };

        await Assert.That(result).IsEqualTo(10);

        instance.Child.Age = 22;
        await Assert.That(result).IsEqualTo(22);

        instance.Child = new ClassB { Age = 25 };
        await Assert.That(result).IsEqualTo(25);

        instance.Child.Age = 26;
        await Assert.That(result).IsEqualTo(26);
        instance.Child = null;
    }

    [Test]
    public async Task NullChildWithInitialValue()
    {
        var instance = new ClassA();

        var chain = instance.WhenPropertyChanged(a => a!.Child!.Age, true);
        int? result = null;

        var subscription = chain.Subscribe(notification => result = notification?.Value);

        await Assert.That(result).IsNull();

        instance.Child = new ClassB { Age = 21 };
        await Assert.That(result).IsEqualTo(21);

        instance.Child.Age = 22;
        await Assert.That(result).IsEqualTo(22);

        instance.Child = new ClassB { Age = 25 };
        await Assert.That(result).IsEqualTo(25);

        instance.Child.Age = 30;
        await Assert.That(result).IsEqualTo(30);
    }

    [Test]
    public async Task NullChildWithoutInitialValue()
    {
        var instance = new ClassA();

        var chain = instance.WhenPropertyChanged(a => a!.Child!.Age, false);
        int? result = null;

        var subscription = chain.Subscribe(notification => result = notification.Value);

        await Assert.That(result).IsNull();

        instance.Child = new ClassB { Age = 21 };
        await Assert.That(result).IsEqualTo(21);

        instance.Child.Age = 22;
        await Assert.That(result).IsEqualTo(22);

        instance.Child = new ClassB { Age = 25 };
        await Assert.That(result).IsEqualTo(25);

        instance.Child.Age = 30;
        await Assert.That(result).IsEqualTo(30);
    }

    [Test]
    public async Task WithoutInitialValue()
    {
        var instance = new ClassA { Name = "TestClass", Child = new ClassB { Age = 10 } };

        var chain = instance.WhenPropertyChanged(a => a!.Child!.Age, false);
        int? result = null;

        var subscription = chain.Subscribe(notification => result = notification.Value);

        await Assert.That(result).IsNull();

        instance.Child.Age = 22;
        await Assert.That(result).IsEqualTo(22);

        instance.Child = new ClassB { Age = 25 };
        await Assert.That(result).IsEqualTo(25);
        instance.Child.Age = 30;
        await Assert.That(result).IsEqualTo(30);
    }

    //  [Test]
    //  [Trait("Manual run for benchmarking","xx")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Manual run for benchmarking")]
    private void StressIt()
    {
        var list = new SourceList<ClassA>();
        var items = Enumerable.Range(1, 10_000).Select(i => new ClassA { Name = i.ToString(), Child = new ClassB { Age = i } }).ToArray();

        list.AddRange(items);

        var sw = new Stopwatch();

        //  var factory =

        var myObservable = list.Connect().Do(_ => sw.Start()).WhenPropertyChanged(a => a!.Child!.Age, false).Do(_ => sw.Stop()).Subscribe();

        if (items.Length > 1 && items[1].Child is not null)
        {
            items[1].Child.Age = -1;
        }
        else
        {
            throw new InvalidOperationException(nameof(items));
        }

        Console.WriteLine($"{sw.ElapsedMilliseconds}");
    }

    public class ClassA : AbstractNotifyPropertyChanged, IEquatable<ClassA>
    {
        private ClassB? _classB;

        private string? _name;

        public ClassB? Child
        {
            get => _classB;
            set => SetAndRaise(ref _classB, value);
        }

        public string? Name
        {
            get => _name;
            set => SetAndRaise(ref _name, value);
        }

        /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.Tests.Binding.DeeplyNestedNotifyPropertyChangedFixture: IDisposable.ClassA" /> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ClassA left, ClassA right) => Equals(left, right);

        /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Tests.Binding.DeeplyNestedNotifyPropertyChangedFixture: IDisposable.ClassA" /> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ClassA left, ClassA right) => !Equals(left, right);

        public bool Equals(ClassA? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(_name, other._name) && Equals(_classB, other._classB);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ClassA)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_name is not null ? _name.GetHashCode() : 0) * 397) ^ (_classB is not null ? _classB.GetHashCode() : 0);
            }
        }

        public override string ToString() => $"ClassA: Name={Name}, {nameof(Child)}: {Child}";
    }

    public class ClassB : AbstractNotifyPropertyChanged, IEquatable<ClassB>
    {
        private int _age;

        public int Age
        {
            get => _age;
            set => SetAndRaise(ref _age, value);
        }

        /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.Tests.Binding.DeeplyNestedNotifyPropertyChangedFixture: IDisposable.ClassB" /> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ClassB left, ClassB right) => Equals(left, right);

        /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Tests.Binding.DeeplyNestedNotifyPropertyChangedFixture: IDisposable.ClassB" /> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ClassB left, ClassB right) => !Equals(left, right);

        public bool Equals(ClassB? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _age == other._age;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ClassB)obj);
        }

        public override int GetHashCode() => _age;

        public override string ToString() => $"{nameof(Age)}: {Age}";
    }

    public class ClassC : AbstractNotifyPropertyChanged
    {
        private ClassD? _classD;

        public ClassD? Child
        {
            get => _classD;
            set => SetAndRaise(ref _classD, value);
        }

        public override string ToString()
            => $"ClassC: {nameof(Child)}={Child}";
    }

    public class ClassD
    {
        public int Value { get; set; }

        public override string ToString()
            => $"ClassD: {nameof(Value)}={Value}";
    }
}
