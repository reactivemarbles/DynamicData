using System;
using System.Collections.Generic;
using DynamicData.Binding;

namespace DynamicData.ReactiveUI.Tests.Domain
{
    public class Person :AbstractNotifyPropertyChanged, IEquatable<Person>
    {
        private int _age;


        public Person(string firstname, string lastname, int age, string gender = "F")
            : this(firstname + " " + lastname, age, gender)
        {
        }

        public Person(string name, int age, string gender = "F")
        {
            Name = name;
            _age = age;
            Gender = gender;

        }
        
        public string Name { get; }
        public string Gender { get; }

        public int Age
        {
            get => _age;
            set => SetAndRaise(ref _age, value);
        }

     


        public override string ToString()
        {
            return $"{Name}. {Age}";
        }

        #region Equality Members



        public static bool operator ==(Person left, Person right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Person left, Person right)
        {
            return !Equals(left, right);
        }


        public bool Equals(Person other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Gender, other.Gender) && _age == other._age;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Person)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Gender != null ? Gender.GetHashCode() : 0) * 397) ^ _age;
            }
        }

        private sealed class NameAgeEqualityComparer : IEqualityComparer<Person>
        {
            public bool Equals(Person x, Person y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.Name, y.Name) && x.Age == y.Age;
            }

            public int GetHashCode(Person obj)
            {
                unchecked
                {
                    return ((obj.Name != null ? obj.Name.GetHashCode() : 0) * 397) ^ obj.Age;
                }
            }
        }

        private static readonly IEqualityComparer<Person> NameAgeComparerInstance = new NameAgeEqualityComparer();


        public static IEqualityComparer<Person> NameAgeComparer
        {
            get { return NameAgeComparerInstance; }
        }



        #endregion


    }
}