using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using BenchmarkDotNet.Attributes;

namespace DynamicData.Benchmarks.Cache;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
public class TransformMany
{
    [Benchmark]
    public void Perf()
    {
        var children = Enumerable
            .Range(1, 10000)
            .Select(i => new Child(
                id: i,
                name: $"Child #{i}"))
            .ToArray();

        var childIndex = 0;
        var parents = Enumerable
            .Range(1, 5000)
            .Select(i => new Parent(
                id: i,
                children: new[]
                {
                    children[childIndex++],
                    children[childIndex++]
                }))
            .ToArray();

        using var source = new SourceCache<Parent, int>(x => x.Id);

        using var subscription = source
            .Connect()
            .TransformMany(p => p.Children, c => c.Name)
            .Subscribe();

        source.AddOrUpdate(parents);
    }

    private class Parent
    {
        public Parent(
            int id,
            IEnumerable<Child> children)
        {
            Id = id;
            Children = children.ToArray();
        }

        public int Id { get; }

        public IReadOnlyList<Child> Children { get; }
    }

    private class Child
    {
        public Child(
            int id,
            string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }
}
