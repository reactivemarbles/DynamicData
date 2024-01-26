using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData.Binding;

namespace DynamicData.Tests.Domain;

internal sealed class AnimalOwner(string name, Guid? id = null, bool include = true) : AbstractNotifyPropertyChanged, IDisposable
{
    private bool _includeInResults = include;
    private readonly SerialDisposable _collectionSubscription = new();
    private ReadOnlyObservableCollection<Animal>? _collection;
    private IObservableCache<Animal, int>? _observableCache;

    public Guid Id { get; } = id ?? Guid.NewGuid();

    public string Name => name;

    public ISourceList<Animal> Animals { get; } = new SourceList<Animal>();

    public ReadOnlyObservableCollection<Animal> ObservableCollection => _collection ??= CreateObservableCollection();

    public IObservableCache<Animal, int> ObservableCache => _observableCache ??= Animals.Connect().AddKey(a => a.Id).AsObservableCache();

    public bool IncludeInResults
    {
        get => _includeInResults;
        set => SetAndRaise(ref _includeInResults, value);
    }

    public void Dispose()
    {
        _collectionSubscription.Dispose();
        _observableCache?.Dispose();
        Animals.Dispose();
    }

    public override string ToString() => $"{Name} [{Animals.Count} Animals] ({Id:B})";

    private ReadOnlyObservableCollection<Animal> CreateObservableCollection()
    {
        _collectionSubscription.Disposable = Animals.Connect().Bind(out var collection).Subscribe();
        return collection;
    }
}
