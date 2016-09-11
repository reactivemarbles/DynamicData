using System;
using System.Collections.Generic;
using DynamicData.Binding;

namespace DynamicData.Tests.Domain
{
    public class Person : AbstractNotifyPropertyChanged, IKey<string>, IEquatable<Person>
    {
        private readonly string _name;
        private int _age;
        private readonly string _gender;

        public Person(string firstname, string lastname, int age, string gender = "F")
            : this(firstname + " " + lastname, age, gender)
        {
        }

        public Person(string name, int age, string gender = "F")
        {
            _name = name;
            _age = age;
            _gender = gender;
        }

        public string Name => _name;

        public string Gender => _gender;

        public int Age { get { return _age; } set { SetAndRaise(ref _age, value); } }

        public string Key => _name;

        public override string ToString()
        {
            return $"{this.Name}. {this.Age}";
        }

        #region Equality Members

        public bool Equals(Person other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_name, other._name);
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
            return _name?.GetHashCode() ?? 0;
        }


        private sealed class AgeEqualityComparer : IEqualityComparer<Person>
        {
            public bool Equals(Person x, Person y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x._age == y._age;
            }

            public int GetHashCode(Person obj)
            {
                return obj._age;
            }
        }

        private static readonly IEqualityComparer<Person> AgeComparerInstance = new AgeEqualityComparer();

        public static IEqualityComparer<Person> AgeComparer => AgeComparerInstance;


        private sealed class NameAgeGenderEqualityComparer : IEqualityComparer<Person>
        {
            public bool Equals(Person x, Person y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x._name, y._name) && x._age == y._age && string.Equals(x._gender, y._gender);
            }

            public int GetHashCode(Person obj)
            {
                unchecked
                {
                    var hashCode = (obj._name != null ? obj._name.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ obj._age;
                    hashCode = (hashCode*397) ^ (obj._gender != null ? obj._gender.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        private static readonly IEqualityComparer<Person> NameAgeGenderComparerInstance = new NameAgeGenderEqualityComparer();

        public static IEqualityComparer<Person> NameAgeGenderComparer
        {
            get { return NameAgeGenderComparerInstance; }
        }

        #endregion


    }
}
