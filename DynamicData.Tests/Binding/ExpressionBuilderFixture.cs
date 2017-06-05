using System;
using System.Diagnostics;
using DynamicData.Binding;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{              

    [TestFixture]
    public class ExpressionBuilderFixture
    {
        [Test]
        public void NotifiesInitialValue()
        {
            var instance = new ClassA {Child = new ClassB {Age = 10}};

            var chain =  instance.ObserveChain(a => a.Child.Age, true);
            int? result = null;

            var subscription = chain.Subscribe(notification => result = notification?.Value);

            result.Should().Be(10);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB {Age = 25};
            result.Should().Be(25);

            instance.Child.Age = 26;
            result.Should().Be(26);
            instance.Child = null;
            result.Should().Be(null);

            instance.Child = new ClassB { Age = 21 };
            result.Should().Be(21);
        }

        [Test]
        public void NotifiesInitialValueAndNullChild()
        {
            var instance = new ClassA();

            var chain = instance.ObserveChain(a => a.Child.Age, true);
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

        [Test]
        public void WithoutInitialValue()
        {
            var instance = new ClassA {Name="TestClass", Child = new ClassB {Age = 10}};

            var chain = instance.ObserveChain(a => a.Child.Age, false);
            int? result = null;

            var subscription = chain.Subscribe(notification => result = notification.Value);

            result.Should().Be(null);

            instance.Child.Age = 22;
            result.Should().Be(22);

            instance.Child = new ClassB {Age = 25};
            result.Should().Be(25);

            instance.Child.Age = 30;
            result.Should().Be(30);
        }

        [Test]
        public void NullChildWithoutInitialValue()
        {
            var instance = new ClassA();

            var chain = instance.ObserveChain(a => a.Child.Age, false);
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

        [Test]
        public void NullChildWithInitialValue()
        {
            var instance = new ClassA();

            var chain = instance.ObserveChain(a => a.Child.Age, true);
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

        [Test]
        public void DepthOfOne()
        {
            var instance = new ClassA {Name="Someone"};

            var chain = instance.ObserveChain(a => a.Name, true);
            string result = null;

            var subscription = chain.Subscribe(notification => result = notification?.Value);

            result.Should().Be("Someone");

            instance.Name = "Else";
            result.Should().Be("Else");

            instance.Name = null;
            result.Should().Be(null);

            instance.Name = "NotNull";
            result.Should().Be("NotNull");

        }

        public class ClassA: AbstractNotifyPropertyChanged, IEquatable<ClassA>
        {
            private string _name;

            public string Name
            {
                get => _name;
                set => SetAndRaise(ref _name, value);
            }

            private ClassB _classB;

            public ClassB Child
            {
                get => _classB;
                set => SetAndRaise(ref _classB, value);
            }

            #region Equality

            public bool Equals(ClassA other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(_name, other._name) && Equals(_classB, other._classB);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ClassA) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((_name != null ? _name.GetHashCode() : 0) * 397) ^ (_classB != null ? _classB.GetHashCode() : 0);
                }
            }

            /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.Tests.Binding.ExpressionBuilderFixture.ClassA" /> objects are equal.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
            public static bool operator ==(ClassA left, ClassA right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Tests.Binding.ExpressionBuilderFixture.ClassA" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ClassA left, ClassA right)
            {
                return !Equals(left, right);
            }

            #endregion

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

            #region Equality

            public bool Equals(ClassB other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return _age == other._age;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ClassB) obj);
            }

            public override int GetHashCode()
            {
                return _age;
            }

            /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.Tests.Binding.ExpressionBuilderFixture.ClassB" /> objects are equal.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
            public static bool operator ==(ClassB left, ClassB right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.Tests.Binding.ExpressionBuilderFixture.ClassB" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ClassB left, ClassB right)
            {
                return !Equals(left, right);
            }

            #endregion

            public override string ToString()
            {
                return $"{nameof(Age)}: {Age}";
            }
        }

    }
}
