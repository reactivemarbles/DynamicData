using System;

namespace DynamicData.Tests.Domain
{
    public class ParentAndChildren : IEquatable<ParentAndChildren>
    {
        public Person Parent { get; }
        public Person[] Children { get; }

        public int Count => Children.Length;

        public ParentAndChildren(Person parent, Person[] children)
        {
            Parent = parent;
            Children = children;
        }

        #region Equality

        public bool Equals(ParentAndChildren other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Parent, other.Parent);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ParentAndChildren) obj);
        }

        public override int GetHashCode()
        {
            return (Parent != null ? Parent.GetHashCode() : 0);
        }

        public static bool operator ==(ParentAndChildren left, ParentAndChildren right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ParentAndChildren left, ParentAndChildren right)
        {
            return !Equals(left, right);
        }

        #endregion

        public override string ToString()
        {
            return $"{nameof(Parent)}: {Parent}, ({Count} children)";
        }
    }
}