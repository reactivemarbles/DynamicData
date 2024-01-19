namespace DynamicData.Tests.Domain;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1066:Implement IEquatable when overriding Object.Equals", Justification = "Acceptable in a test.")]
public readonly struct PersonEmpKey
{
    private readonly string _name;

    private readonly string _company;

    public PersonEmpKey(string name, string company)
    {
        _name = name;
        _company = company;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Acceptable in a test.")]
    public PersonEmpKey(PersonEmployment personEmployment)
    {
        _name = personEmployment.Name;
        _company = personEmployment.Company;
    }

    public bool Equals(PersonEmpKey other) => string.Equals(_name, other._name) && string.Equals(_company, other._company);

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is PersonEmpKey personEmpKey && Equals(personEmpKey);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((_name is not null ? _name.GetHashCode() : 0) * 397) ^ (_company is not null ? _company.GetHashCode() : 0);
        }
    }

    public static bool operator ==(PersonEmpKey left, PersonEmpKey right) => left.Equals(right);

    public static bool operator !=(PersonEmpKey left, PersonEmpKey right) => !(left == right);
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

        return Equals((PersonEmployment)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((Name is not null ? Name.GetHashCode() : 0) * 397) ^ (Company is not null ? Company.GetHashCode() : 0);
        }
    }

    public override string ToString() => string.Format("Name: {0}, Company: {1}", Name, Company);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Acceptable in a test.")]
    protected bool Equals(PersonEmployment other) => string.Equals(Name, other.Name) && string.Equals(Company, other.Company);
}
