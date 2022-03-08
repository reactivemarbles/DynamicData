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

public class Animal : AbstractNotifyPropertyChanged
{
    private bool _includeInResults;

    public Animal(string name, string type, AnimalFamily family)
    {
        Name = name;
        Type = type;
        Family = family;
    }

    public AnimalFamily Family { get; }

    public bool IncludeInResults
    {
        get => _includeInResults;
        set => SetAndRaise(ref _includeInResults, value);
    }

    public string Name { get; }

    public string Type { get; }
}
