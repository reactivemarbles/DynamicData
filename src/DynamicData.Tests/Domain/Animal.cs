using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
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

public sealed class Animal(string name, string type, AnimalFamily family, bool include = true, int? id = null) : AbstractNotifyPropertyChanged
{
    private static int s_counter;

    private bool _includeInResults = include;

    public int Id { get; } = id ?? Interlocked.Increment(ref s_counter);

    public string Name { get; } = name;

    public AnimalFamily Family { get; } = family;

    public string Type { get; } = type;

    public string FormalName => $"{Name} the {Type}";

    public bool IncludeInResults
    {
        get => _includeInResults;
        set => SetAndRaise(ref _includeInResults, value);
    }

    public override string ToString() => $"{FormalName} ({Family}) [{Id:x4}]";

    public override int GetHashCode() => HashCode.Combine(Id, Name, Family, Type);

    public static IComparer<Animal> NameComparer { get; } = new AnimalAlphabeticComparer();

    public static IEqualityComparer<Animal> NameTypeCompare { get; } = new AnimalEqualityComparer();

    public static IEqualityComparer<Animal> IdCompare { get; } = new AnimalIdComparer();

    private sealed class AnimalAlphabeticComparer : IComparer<Animal>
    {
        public int Compare([DisallowNull] Animal x, [DisallowNull] Animal y) => (x, y) switch
        {
            (null, null) => 0,
            (Animal a, Animal b) => string.Compare(a.FormalName, b.FormalName, StringComparison.OrdinalIgnoreCase),
            (null, _) => 1,
            _ => -1
        };
    }

    private sealed class AnimalIdComparer : IEqualityComparer<Animal>
    {
        public bool Equals([DisallowNull] Animal x, [DisallowNull] Animal y) => (x, y) switch
        {
            (null, null) => true,
            (Animal a, Animal b) => a.Id == b.Id,
            _ => false,
        };

        public int GetHashCode([DisallowNull] Animal obj) => HashCode.Combine(obj?.Id ?? 0);
    }

    private sealed class AnimalEqualityComparer : IEqualityComparer<Animal>
    {
        public bool Equals(Animal? x, Animal? y) => (x, y) switch
        {
            (null, null) => true,
            (Animal a, Animal b) => (a.Type == b.Type) && (a.Family == b.Family) && (a.Name == b.Name),
            _ => false,
        };

        public int GetHashCode([DisallowNull] Animal obj) => HashCode.Combine(obj?.Name ?? string.Empty, obj.Type, obj.Family);
    }
}
