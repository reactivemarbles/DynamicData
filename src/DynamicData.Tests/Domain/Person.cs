using System;
using System.Collections.Generic;

using DynamicData.Binding;

namespace DynamicData.Tests.Domain;

public enum Color
{
    NotSpecified,
    Red,
    Orange,
    Yellow,
    Green,
    Blue,
    Indigo,
    Violet,
}

public class Person : AbstractNotifyPropertyChanged, IEquatable<Person>, IComparable<Person>
{
    private int _age;
    private int? _ageNullable;
    private Color _favoriteColor;
    private AnimalFamily _petType;

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

    private Person(string name, int? age, string gender, Person personCopyKey)
    {
        Name = name;
        _ageNullable = age;
        Gender = gender;
        ParentName = personCopyKey?.ParentName ?? throw new ArgumentNullException(nameof(personCopyKey));
        UniqueKey = personCopyKey.UniqueKey;
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

    public Color FavoriteColor
    {
        get => _favoriteColor;
        set => SetAndRaise(ref _favoriteColor, value);
    }

    public AnimalFamily PetType
    {
        get => _petType;
        set => SetAndRaise(ref _petType, value);
    }

    public string Gender { get; }

    public string Key => Name;

    public string UniqueKey { get; } = Guid.NewGuid().ToString("B");

    public string Name { get; }

    public string ParentName { get; }

    public static bool operator ==(Person left, Person right) => Equals(left, right);

    public static bool operator !=(Person left, Person right) => !Equals(left, right);

    public static Person CloneUniqueId(Person sourceData, Person sourceId) =>
        new((sourceData ?? throw new ArgumentNullException(nameof(sourceData))).Name, sourceData.Age, sourceData.Gender, sourceId)
        {
            FavoriteColor = sourceData.FavoriteColor,
            PetType = sourceData.PetType,
        };

    public bool Equals(Person? other)
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

        return Equals((Person)obj);
    }

    public override int GetHashCode() => (Name is not null ? Name.GetHashCode() : 0);

    public override string ToString() => $"{Name}. {Age}";

    private sealed class AgeEqualityComparer : IEqualityComparer<Person>
    {
        public bool Equals(Person? x, Person? y)
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

            return x._age == y._age;
        }

        public int GetHashCode(Person obj) => obj._age;
    }

    private sealed class NameAgeGenderEqualityComparer : IEqualityComparer<Person>
    {
        public bool Equals(Person? x, Person? y)
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

    // Implemented for SortAndBindFixture.
    public static IComparer<Person> DefaultComparer { get; } = SortExpressionComparer<Person>
        .Ascending(p => p.Age)
        .ThenByAscending(p => p.Name);

    public int CompareTo(Person? other) => DefaultComparer.Compare(this, other);
}
