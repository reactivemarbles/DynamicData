using System;

using DynamicData.Kernel;

namespace DynamicData.Tests.Domain;

public class ParentAndChildren : IEquatable<ParentAndChildren>
{
    public ParentAndChildren(Person parent, Person[] children)
    {
        Parent = parent;
        Children = children;
    }

    public ParentAndChildren(string parentId, Optional<Person> parent, Person[] children)
    {
        Parent = parent.ValueOrDefault();
        ParentId = parentId;
        Children = children;
    }

    public Person[] Children { get; }

    public int Count => Children.Length;

    public Person? Parent { get; }

    public string? ParentId { get; }

    public static bool operator ==(ParentAndChildren left, ParentAndChildren right) => Equals(left, right);

    public static bool operator !=(ParentAndChildren left, ParentAndChildren right) => !Equals(left, right);

    public bool Equals(ParentAndChildren? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(ParentId, other.ParentId);
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

        return Equals((ParentAndChildren)obj);
    }

    public override int GetHashCode() => (ParentId is not null ? ParentId.GetHashCode() : 0);

    public override string ToString() => $"{nameof(Parent)}: {Parent}, ({Count} children)";
}
