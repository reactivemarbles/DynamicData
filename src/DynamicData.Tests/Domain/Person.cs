using System;
using System.Collections.Generic;

using DynamicData.Binding;

namespace DynamicData.Tests.Domain;

public class Person : AbstractNotifyPropertyChanged, IEquatable<Person>
{
    private int _age;
    private int? _ageNullable;

    public Person()
        : this("unknown", 0, "none")
    {

    }
    public Person(string firstname, string lastname, int age, string gender = "F", string? parentName = null)
        : this(firstname + " " + lastname, age, gender, parentName)
    {
    }

    public Person(string name, int age, string gender = "F", string? parentName = null)
    {
        Name = name;
        _age = age;
        Gender = gender;
        ParentName = parentName ?? string.Empty;
    }

    public Person(string name, int? age, string gender = "F", string? parentName = null)
    {
        Name = name;
        _ageNullable = age;
        Gender = gender;
        ParentName = parentName ?? string.Empty;
    }

    public static IEqualityComparer<Person> AgeComparer { get; } = new AgeEqualityComparer();

    public static IEqualityComparer<Person> NameAgeGenderComparer { get; } = new NameAgeGenderEqualityComparer();

    public int Age
    {
        get => _age;
        set => SetAndRaise(ref _age, value);
    }

    public int? AgeNullable
    {
        get => _ageNullable;
        set => SetAndRaise(ref _ageNullable, value);
    }

    public string Gender { get; }

    public string Key => Name;

    public string Name { get; }

    public string ParentName { get; }

    public static bool operator ==(Person left, Person right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(Person left, Person right)
    {
        return !Equals(left, right);
    }

    public bool Equals(Person? other)
    {
        if (ReferenceEquals(null, other))
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

        return Equals((Person)obj);
    }

    public override int GetHashCode()
    {
        return (Name is not null ? Name.GetHashCode() : 0);
    }

    public override string ToString()
    {
        return $"{Name}. {Age}";
    }

    private sealed class AgeEqualityComparer : IEqualityComparer<Person>
    {
        public bool Equals(Person? x, Person? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return x._age == y._age;
        }

        public int GetHashCode(Person obj)
        {
            return obj._age;
        }
    }

    private sealed class NameAgeGenderEqualityComparer : IEqualityComparer<Person>
    {
        public bool Equals(Person? x, Person? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null))
            {
                return false;
            }

            if (ReferenceEquals(y, null))
            {
                return false;
            }

            if (x.GetType() != y.GetType())
            {
                return false;
            }

            return string.Equals(x.Name, y.Name) && x._age == y._age && string.Equals(x.Gender, y.Gender);
        }

        public int GetHashCode(Person obj)
        {
            unchecked
            {
                var hashCode = obj.Name.GetHashCode();
                hashCode = (hashCode * 397) ^ obj._age;
                hashCode = (hashCode * 397) ^ obj.Gender.GetHashCode();
                return hashCode;
            }
        }
    }
}
