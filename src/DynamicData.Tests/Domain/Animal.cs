using DynamicData.Binding;

namespace DynamicData.Tests.Domain;

public enum AnimalFamily
{
    Mammal,

    Reptile,

    Fish,

    Amphibian,

    Bird
}

public class Animal(string name, string type, AnimalFamily family, bool include = true) : AbstractNotifyPropertyChanged
{
    private bool _includeInResults = include;

    public AnimalFamily Family { get; } = family;

    public bool IncludeInResults
    {
        get => _includeInResults;
        set => SetAndRaise(ref _includeInResults, value);
    }

    public string Name { get; } = name;

    public string Type { get; } = type;
}
