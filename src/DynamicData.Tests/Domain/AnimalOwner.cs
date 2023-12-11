using System;
using DynamicData.Binding;

namespace DynamicData.Tests.Domain;

internal class AnimalOwner(string name, Guid? id = null, bool include = true) : AbstractNotifyPropertyChanged, IDisposable
{
    private bool _includeInResults = include;

    public Guid Id { get; } = id ?? Guid.NewGuid();

    public string Name => name;

    public ISourceList<Animal> Animals { get; } = new SourceList<Animal>();

    public bool IncludeInResults
    {
        get => _includeInResults;
        set => SetAndRaise(ref _includeInResults, value);
    }

    public void Dispose() => Animals.Dispose();

    public override string ToString() => $"{Name} [{Animals.Count} Animals] ({Id:B})";
}
