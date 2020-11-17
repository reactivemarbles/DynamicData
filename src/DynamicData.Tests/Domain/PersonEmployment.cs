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

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

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
        public PersonEmployment(string name, string company)
        {
            Name = name;
            Company = company;
            Key = new PersonEmpKey(this);
        }

        public string Company { get; }

        public PersonEmpKey Key { get; }

        public string Name { get; }

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

            return Equals((PersonEmployment)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Company != null ? Company.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Company: {1}", Name, Company);
        }

        protected bool Equals(PersonEmployment other)
        {
            return string.Equals(Name, other.Name) && string.Equals(Company, other.Company);
        }
    }
}