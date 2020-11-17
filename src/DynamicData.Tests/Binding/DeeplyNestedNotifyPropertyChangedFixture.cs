using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding
{
    public class DeeplyNestedNotifyPropertyChangedFixture
    {
        [Fact]
        public void DepthOfOne()
        {
            var instance = new ClassA { Name = "Someone" };

            var chain = instance.WhenPropertyChanged(a => a.Name, true);
            string? result = null;

            var subscription = chain.Subscribe(notification => result = notification?.Value);

            result.Should().Be("Someone");

            instance.Name = "Else";
            result.Should().Be("Else");

            instance.Name = null;
            result.Should().Be(null);

            instance.Name = "NotNull";
            result.Should().Be("NotNull");
        }

        [Fact]
        public void NotifiesInitialValue_WithFallback()
        {
            var instance = new ClassA { Child = new ClassB { Age = 10 } };

            //provide a fallback so a value can always be obtained
            var chain = instance.WhenChanged(a => a!.Child!.Age, (sender, a) => a, () => -1);

            int? result = null;

            var subscription = chain.Subscribe(age => result = age);

            result.Should().Be(10);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB { Age = 25 };
            result.Should().Be(25);

            instance.Child.Age = 26;
            result.Should().Be(26);
            instance.Child = null;
            result.Should().Be(-1);

            instance.Child = new ClassB { Age = 21 };
            result.Should().Be(21);
        }

        [Fact]
        public void NotifiesInitialValueAndNullChild()
        {
            var instance = new ClassA();

            var chain = instance.WhenPropertyChanged(a => a.Child!.Age, true);
            int? result = null;

            var subscription = chain.Subscribe(notification => result = notification?.Value);
            result.Should().Be(null);
            instance.Child = new ClassB { Age = 10 };

            result.Should().Be(10);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB { Age = 25 };
            result.Should().Be(25);

            instance.Child.Age = 26;
            result.Should().Be(26);
            instance.Child = null;
        }

        [Fact]
        public void NullChildWithInitialValue()
        {
            var instance = new ClassA();

            var chain = instance.WhenPropertyChanged(a => a!.Child!.Age, true);
            int? result = null;

            var subscription = chain.Subscribe(notification => result = notification?.Value);

            result.Should().Be(null);

            instance.Child = new ClassB { Age = 21 };
            result.Should().Be(21);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB { Age = 25 };
            result.Should().Be(25);

            instance.Child.Age = 30;
            result.Should().Be(30);
        }

        [Fact]
        public void NullChildWithoutInitialValue()
        {
            var instance = new ClassA();

            var chain = instance.WhenPropertyChanged(a => a!.Child!.Age, false);
            int? result = null;

            var subscription = chain.Subscribe(notification => result = notification.Value);

            result.Should().Be(null);

            instance.Child = new ClassB { Age = 21 };
            result.Should().Be(21);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB { Age = 25 };
            result.Should().Be(25);

            instance.Child.Age = 30;
            result.Should().Be(30);
        }

        [Fact]
        public void WithoutInitialValue()
        {
            var instance = new ClassA { Name = "TestClass", Child = new ClassB { Age = 10 } };

            var chain = instance.WhenPropertyChanged(a => a!.Child!.Age, false);
            int? result = null;

            var subscription = chain.Subscribe(notification => result = notification.Value);

            result.Should().Be(null);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB { Age = 25 };
            result.Should().Be(25);
            instance.Child.Age = 30;
            result.Should().Be(30);
        }

        //  [Fact]
        //  [Trait("Manual run for benchmarking","xx")]
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
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                items[1].Child.Age = -1;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
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
            public static bool operator ==(ClassA left, ClassA right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Tests.Binding.DeeplyNestedNotifyPropertyChangedFixture: IDisposable.ClassA" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ClassA left, ClassA right)
            {
                return !Equals(left, right);
            }

            public bool Equals(ClassA? other)
            {
                if (ReferenceEquals(null, other))
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
                if (ReferenceEquals(null, obj))
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
                    return ((_name != null ? _name.GetHashCode() : 0) * 397) ^ (_classB is not null ? _classB.GetHashCode() : 0);
                }
            }

            public override string ToString()
            {
                return $"ClassA: Name={Name}, {nameof(Child)}: {Child}";
            }
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
            public static bool operator ==(ClassB left, ClassB right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Tests.Binding.DeeplyNestedNotifyPropertyChangedFixture: IDisposable.ClassB" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ClassB left, ClassB right)
            {
                return !Equals(left, right);
            }

            public bool Equals(ClassB? other)
            {
                if (ReferenceEquals(null, other))
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
                if (ReferenceEquals(null, obj))
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

            public override int GetHashCode()
            {
                return _age;
            }

            public override string ToString()
            {
                return $"{nameof(Age)}: {Age}";
            }
        }
    }
}