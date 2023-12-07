using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Domain;

public class PersonWithChildren : IKey<string>
{
    public PersonWithChildren(string name, int age)
        : this(name, age, Enumerable.Empty<Person>())
    {
    }

    public PersonWithChildren(string name, int age, IEnumerable<Person> relations)
    {
        Name = name;
        Age = age;
        KeyValue = Name;
        Relations = relations;
        Key = name;
    }

    public int Age { get; set; }

    /// <summary>
    ///     The key
    /// </summary>
    public string Key { get; }

    public string KeyValue { get; }

    public string Name { get; }

    public IEnumerable<Person> Relations { get; }

    public override string ToString() => $"{Name}. {Age}";
}
