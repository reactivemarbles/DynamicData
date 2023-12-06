using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using BenchmarkDotNet.Attributes;

using DynamicData.Kernel;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class EditDiff
{
    public const int MaxItems
        = 1097;

    [Benchmark]
    [Arguments(7, 3, 5)]
    [Arguments(233, 113, MaxItems)]
    [Arguments(233, 0, MaxItems)]
    [Arguments(233, 233, MaxItems)]
    [Arguments(2521, 1187, MaxItems)]
    [Arguments(2521, 0, MaxItems)]
    [Arguments(2521, 2521, MaxItems)]
    [Arguments(5081, 2683, MaxItems)]
    [Arguments(5081, 0, MaxItems)]
    [Arguments(5081, 5081, MaxItems)]
    public void AddsRemovesAndUpdates(int collectionSize, int updateSize, int maxItems)
    {
        using var subscription = Enumerable
            .Range(1, maxItems - 1)
            .Select(n => n * (collectionSize - updateSize))
            .Select(index => Person.CreateRange(index, updateSize, "Overlap")
                .Concat(Person.CreateRange(index + updateSize, collectionSize - updateSize, "Name")))
            .Prepend(Person.CreateRange(0, collectionSize, "Name"))
            .ToObservable()
            .EditDiff(p => p.Id)
            .Subscribe();
    }

    [Benchmark]
    [Arguments(7)]
    [Arguments(MaxItems)]
    public void OptionalAddsAndRemoves(int maxItems)
    {
        using var subscription = Enumerable
            .Range(0, MaxItems)
            .Select(n => (n % 2) == 0
                ? new Person(n, "Name")
                : Optional.None<Person>())
            .ToObservable()
            .EditDiff(p => p.Id)
            .Subscribe();
    }

    private class Person
    {
        public static IReadOnlyList<Person> CreateRange(int baseId, int count, string baseName)
            => Enumerable
                .Range(baseId, count)
                .Select(i => new Person(i, baseName + i))
                .ToArray();

        public Person(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }
}
