using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace DynamicData.Tests.Domain;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Acceptable in test.")]
public class PersonObs(string name, int age, string gender = "F", string? parentName = null) : IEquatable<PersonObs>
{
    private readonly BehaviorSubject<int> _age = new BehaviorSubject<int>(age);

    public PersonObs(string firstname, string lastname, int age, string gender = "F", string? parentName = null)
        : this(firstname + " " + lastname, age, gender, parentName)
    {
    }

    public static IEqualityComparer<PersonObs> AgeComparer { get; } = new AgeEqualityComparer();

    public static IEqualityComparer<PersonObs> NameAgeGenderComparer { get; } = new NameAgeGenderEqualityComparer();

    public IObservable<int> Age => _age;

    public string Gender { get; } = gender;

    public string Key => Name;

    public string Name { get; } = name;

    public string ParentName { get; } = parentName ?? string.Empty;

    public static bool operator ==(PersonObs left, PersonObs right) => Equals(left, right);

    public static bool operator !=(PersonObs left, PersonObs right) => !Equals(left, right);

    public bool Equals(PersonObs? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(Name, other.Name);
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

        return Equals((PersonObs)obj);
    }

    public override int GetHashCode() => (Name is not null ? Name.GetHashCode() : 0);

    public void SetAge(int age) => _age.OnNext(age);

    public override string ToString() => $"{Name}. {_age.Value}";

    private sealed class AgeEqualityComparer : IEqualityComparer<PersonObs>
    {
        public bool Equals(PersonObs? x, PersonObs? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x._age.Value == y._age.Value;
        }

        public int GetHashCode(PersonObs obj) => obj._age.Value;
    }

    private sealed class NameAgeGenderEqualityComparer : IEqualityComparer<PersonObs>
    {
        public bool Equals(PersonObs? x, PersonObs? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return false;
            }

            if (y is null)
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return string.Equals(x.Name, y.Name) && x._age == y._age && string.Equals(x.Gender, y.Gender);
        }

        public int GetHashCode(PersonObs obj)
        {
            unchecked
            {
                var hashCode = (obj.Name is not null ? obj.Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ obj._age.Value;
                hashCode = (hashCode * 397) ^ (obj.Gender is not null ? obj.Gender.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
