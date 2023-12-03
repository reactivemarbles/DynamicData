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

public class Animal(string name, string type, AnimalFamily family) : AbstractNotifyPropertyChanged
{
    private bool _includeInResults;

    public AnimalFamily Family { get; } = family;

    public bool IncludeInResults
    {
        get => _includeInResults;
        set => SetAndRaise(ref _includeInResults, value);
    }

    public string Name { get; } = name;

    public string Type { get; } = type;
}
