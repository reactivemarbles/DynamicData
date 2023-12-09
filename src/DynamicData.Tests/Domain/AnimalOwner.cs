
using System;

namespace DynamicData.Tests.Domain;

internal class AnimalOwner(string name, Guid? id = null) : IDisposable
{
    public Guid Id { get; } = id ?? Guid.NewGuid();

    public string Name => name;

    public ISourceList<Animal> Animals { get; } = new SourceList<Animal>();

    public void Dispose() => Animals.Dispose();
}
