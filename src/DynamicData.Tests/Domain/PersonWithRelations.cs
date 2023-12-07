using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Domain;

public class PersonWithRelations : IKey<string>
{
    public PersonWithRelations(string name, int age)
        : this(name, age, Enumerable.Empty<PersonWithRelations>())
    {
    }

    public PersonWithRelations(string name, int age, IEnumerable<PersonWithRelations> relations)
    {
        Name = name;
        Age = age;
        KeyValue = Name;
        Relations = relations;
        Key = name;
        Pet = Enumerable.Empty<Pet>();
    }

    public int Age { get; }

    /// <summary>
    ///     The key
    /// </summary>
    public string Key { get; }

    public string KeyValue { get; }

    public string Name { get; }

    public IEnumerable<Pet> Pet { get; set; }

    public IEnumerable<PersonWithRelations> Relations { get; }

    public override string ToString() => $"{Name}. {Age}";
}
