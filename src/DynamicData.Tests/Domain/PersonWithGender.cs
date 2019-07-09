using System;

namespace DynamicData.Tests.Domain
{
    public class PersonWithGender : IEquatable<PersonWithGender>
    {
        private readonly string _name;
        private readonly int _age;
        private readonly string _gender;

        public PersonWithGender(Person person, string gender)
        {
            _name = person.Name;
            _age = person.Age;
            _gender = gender;
        }

        public string Name { get { return _name; } }

        public int Age { get { return _age; } }

        public string Gender { get { return _gender; } }

        public PersonWithGender(string name, int age, string gender)
        {
            _name = name;
            _age = age;
            _gender = gender;
        }

        #region Equality Members

        public bool Equals(PersonWithGender other)
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
                int result = (Name != null ? Name.GetHashCode() : 0);
                result = (result * 397) ^ Age;
                result = (result * 397) ^ (Gender != null ? Gender.GetHashCode() : 0);
                return result;
            }
        }

        #endregion

        public override string ToString()
        {
            return $"{this.Name}. {this.Age} ({Gender})";
        }
    }
}
