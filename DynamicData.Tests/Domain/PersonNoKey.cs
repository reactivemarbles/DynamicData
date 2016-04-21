using System.Collections.Generic;

namespace DynamicData.Tests.Domain
{
    public class PersonNoKey
    {
        private readonly string _name;
        private int _age;
        private readonly string _keyValue;

        public PersonNoKey(string name, int age)
        {
            _name = name;
            _age = age;
            _keyValue = this.Name;
        }

        public string Name { get { return _name; } }

        public int Age { get { return _age; } set { _age = value; } }

        public string KeyValue { get { return _keyValue; } }

        public override string ToString()
        {
            return string.Format("{0}. {1}", this.Name, this.Age);
        }

        #region Equality Members

        public bool Equals(PersonNoKey other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return Equals(other.Name, Name) && other.Age == Age;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != typeof(PersonNoKey))
            {
                return false;
            }
            return Equals((PersonNoKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ Age;
            }
        }

        private sealed class NameAgeEqualityComparer : IEqualityComparer<PersonNoKey>
        {
            public bool Equals(PersonNoKey x, PersonNoKey y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                if (x.GetType() != y.GetType()) return false;
                return string.Equals(x.Name, y.Name) && x.Age == y.Age;
            }

            public int GetHashCode(PersonNoKey obj)
            {
                unchecked
                {
                    return ((obj.Name != null ? obj.Name.GetHashCode() : 0) * 397) ^ obj.Age;
                }
            }
        }

        private static readonly IEqualityComparer<PersonNoKey> NameAgeComparerInstance = new NameAgeEqualityComparer();

        public static IEqualityComparer<PersonNoKey> NameAgeComparer { get { return NameAgeComparerInstance; } }

        #endregion
    }
}
