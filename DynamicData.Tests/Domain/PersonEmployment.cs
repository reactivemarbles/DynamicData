using System.Reactive;

namespace DynamicData.Tests.Domain
{
    public struct PersonEmpKey
    {
        private readonly string _name;
        private readonly string _company;

        public PersonEmpKey(string name, string company)
        {
            _name = name;
            _company = company;
        }

        public PersonEmpKey(PersonEmployment personEmployment)
        {
            _name = personEmployment.Name;
            _company = personEmployment.Company;
        }

        public bool Equals(PersonEmpKey other)
        {
            return string.Equals(_name, other._name) && string.Equals(_company, other._company);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PersonEmpKey && Equals((PersonEmpKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_name != null ? _name.GetHashCode() : 0) * 397) ^ (_company != null ? _company.GetHashCode() : 0);
            }
        }
    }

    public class PersonEmployment : IKey<PersonEmpKey>
    {
        private readonly string _name;
        private readonly string _company;
        private readonly PersonEmpKey _key;

        public PersonEmployment(string name, string company)
        {
            _name = name;
            _company = company;
            _key = new PersonEmpKey(this);
        }

        public string Name => _name;

        public string Company => _company;

        public PersonEmpKey Key => _key;

        protected bool Equals(PersonEmployment other)
        {
            return string.Equals(_name, other._name) && string.Equals(_company, other._company);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PersonEmployment)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_name != null ? _name.GetHashCode() : 0) * 397) ^ (_company != null ? _company.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Company: {1}", _name, _company);
        }
    }
}
