using System;
using System.Collections.Generic;
using DynamicData.Binding;

namespace DynamicData.Tests.Domain
{
    public class Person : AbstractNotifyPropertyChanged, IEquatable<Person>
//, IEquatable<Person>
    {
        public string ParentName { get; set; }
        private readonly string _name;
        private int _age;
        private readonly string _gender;

        public Person(string firstname, string lastname, int age, string gender = "F", string parentName = null)
            : this(firstname + " " + lastname, age, gender, parentName)
        {

        }

        public Person(string name, int age, string gender = "F", string parentName = null)
        {
            _name = name;
            _age = age;
            _gender = gender;
            ParentName = parentName ?? String.Empty;
        }

        public string Name => _name;

        public string Gender => _gender;

        public int Age
        {
            get => _age;
            set { SetAndRaise(ref _age, value); }
        }

        public string Key => _name;

        //public override string ToString()
        //{
        //    return $"{this.Name}. {this.Age}";
        //}

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
            return Equals((Person) obj);
        }

        public override int GetHashCode()
        {
            return (_name != null ? _name.GetHashCode() : 0);
        }

        public static bool operator ==(Person left, Person right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Person left, Person right)
        {
            return !Equals(left, right);
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

        public static IEqualityComparer<Person> AgeComparer { get; } = new AgeEqualityComparer();


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
                    hashCode = (hashCode * 397) ^ obj._age;
                    hashCode = (hashCode * 397) ^ (obj._gender != null ? obj._gender.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        public static IEqualityComparer<Person> NameAgeGenderComparer { get; } = new NameAgeGenderEqualityComparer();

        #endregion
    }
}
