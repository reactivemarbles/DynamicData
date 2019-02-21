using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using DynamicData.Annotations;
using DynamicData.Binding;

namespace DynamicData.Tests.Domain
{
    public class PersonObs : IEquatable<PersonObs>
    {
        public string ParentName { get; }
        public string Name { get; }
        public string Gender { get; }
        public string Key => Name;
        [NotNull]
        private readonly BehaviorSubject<int> _age;

        public PersonObs(string firstname, string lastname, int age, string gender = "F", string parentName = null)
            : this(firstname + " " + lastname, age, gender, parentName)
        {
        }

        public PersonObs(string name, int age, string gender = "F", string parentName = null)
        {
            Name = name;
            _age = new BehaviorSubject<int>(age);
            Gender = gender;
            ParentName = parentName ?? string.Empty;
        }

        public IObservable<int> Age => _age;

        public void SetAge(int age)
        {
            _age.OnNext(age);
        }

        #region Equality Members

        public bool Equals(PersonObs other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PersonObs)obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public static bool operator ==(PersonObs left, PersonObs right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PersonObs left, PersonObs right)
        {
            return !Equals(left, right);
        }

        private sealed class AgeEqualityComparer : IEqualityComparer<PersonObs>
        {
            public bool Equals(PersonObs x, PersonObs y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return x._age.Value == y._age.Value;
            }

            public int GetHashCode(PersonObs obj)
            {
                return obj._age.Value;
            }
        }

        public static IEqualityComparer<PersonObs> AgeComparer { get; } = new AgeEqualityComparer();


        private sealed class NameAgeGenderEqualityComparer : IEqualityComparer<PersonObs>
        {
            public bool Equals(PersonObs x, PersonObs y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.Name, y.Name) && x._age == y._age && string.Equals(x.Gender, y.Gender);
            }

            public int GetHashCode(PersonObs obj)
            {
                unchecked
                {
                    var hashCode = (obj.Name != null ? obj.Name.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ obj._age.Value;
                    hashCode = (hashCode * 397) ^ (obj.Gender != null ? obj.Gender.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        public static IEqualityComparer<PersonObs> NameAgeGenderComparer { get; } = new NameAgeGenderEqualityComparer();

        #endregion

        public override string ToString()
        {
            return $"{Name}. {_age.Value}";
        }
    }
}
