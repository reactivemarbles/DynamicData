using System;
using System.Collections.Generic;


namespace DynamicData.Tests.Domain
{
    public class Person : IKey<string>, IEquatable<Person>
    {
        private readonly string _name;
        private  int _age;
        private readonly string _gender;



        public Person(string firstname, string lastname, int age, string gender = "F")
            : this(firstname + " " + lastname, age, gender)
        {
        }

        public Person(string name, int age,string gender = "F")
        {
            _name= name;
            _age = age;
            _gender = gender;

        }


        public string Name
        {
            get { return _name; }
        }

        public string Gender
        {
            get { return _gender; }
        }


        public int Age
        {
            get { return _age; }
            set { _age = value; }
        }

        public string Key
        {
            get { return _name; }
        }


        public override string ToString()
        {
            return string.Format("{0}. {1}", this.Name, this.Age);
        }

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

        #endregion


    }

}
