
using System;

namespace DynamicData.Tests.Domain;

internal class AnimalOwner(string name) : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name => name;

    public ISourceList<Animal> Animals { get; } = new SourceList<Animal>();

    public void Dispose() => Animals.Dispose();
}
