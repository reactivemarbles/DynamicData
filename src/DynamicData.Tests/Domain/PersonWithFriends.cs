using System.Collections.Generic;
using System.Linq;

using DynamicData.Binding;

namespace DynamicData.Tests.Domain;

public class PersonWithFriends(string name, int age, IEnumerable<PersonWithFriends> friends) : AbstractNotifyPropertyChanged, IKey<string>
{
    private int _age = age;

    private IEnumerable<PersonWithFriends> _friends = friends;

    public PersonWithFriends(string name, int age)
        : this(name, age, Enumerable.Empty<PersonWithFriends>())
    {
    }

    public int Age
    {
        get => _age;
        set => SetAndRaise(ref _age, value);
    }

    public IEnumerable<PersonWithFriends> Friends
    {
        get => _friends;
        set => SetAndRaise(ref _friends, value);
    }

    /// <summary>
    ///     The key
    /// </summary>
    public string Key { get; } = name;

    public string Name { get; } = name;

    public override string ToString() => $"{Name}. {Age}";
}
