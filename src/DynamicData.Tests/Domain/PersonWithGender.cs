using System;

namespace DynamicData.Tests.Domain;

public class PersonWithGender : IEquatable<PersonWithGender>
{
    public PersonWithGender(Person person, string gender)
    {
        Name = person.Name;
        Age = person.Age;
        Gender = gender;
    }

    public PersonWithGender(string name, int age, string gender)
    {
        Name = name;
        Age = age;
        Gender = gender;
    }

    public int Age { get; }

    public string Gender { get; }

    public string Name { get; }

    public bool Equals(PersonWithGender? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Equals(other.Name, Name) && other.Age == Age && Equals(other.Gender, Gender);
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

        if (obj.GetType() != typeof(PersonWithGender))
        {
            return false;
        }

        return Equals((PersonWithGender)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var result = (Name is not null ? Name.GetHashCode() : 0);
            result = (result * 397) ^ Age;
            result = (result * 397) ^ (Gender is not null ? Gender.GetHashCode() : 0);
            return result;
        }
    }

    public override string ToString()
    {
        return $"{this.Name}. {this.Age} ({Gender})";
    }
}
