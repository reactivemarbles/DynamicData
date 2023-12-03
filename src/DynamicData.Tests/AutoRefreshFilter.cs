using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Binding;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests;

public class AutoRefreshFilter
{
    [Fact]
    public void Bind_Transform_and_FilterOnObservable()
    {
        var count = 3;
        var list = new SourceList<string>();
        list.AddRange(Enumerable.Range(1, count).Select(c => $"item {c}"));
        
        var bindedList = new ObservableCollectionExtended<string>();
       
        list.Connect()
            .FilterOnObservable(_ => Observable.Return(true))
            .Transform(str => str)
            .Bind(bindedList)
            .Subscribe(
                _ => { },
                ex => {Assert.Fail("There should be no error");}
            );
    }


    [Fact]
    public void Test()
    {
        var a0 = new Item("A0");
        var i1 = new Item("I1");
        var i2 = new Item("I2");
        var i3 = new Item("I3");

        var obsList = new SourceList<Item>();
        obsList.AddRange(new[] { a0, i1, i2, i3 });

        var obsListDerived = obsList.Connect().AutoRefresh(x => x.Name).Filter(x => x.Name.Contains("I")).AsObservableList();

        obsListDerived.Count.Should().Be(3);
        obsListDerived.Items.Should().BeEquivalentTo(new []{ i1, i2, i3});

        i1.Name = "X2";
        obsListDerived.Count.Should().Be(2);
        obsListDerived.Items.Should().BeEquivalentTo(new[] { i2, i3});

        a0.Name = "I0";
        obsListDerived.Count.Should().Be(3);
        obsListDerived.Items.Should().BeEquivalentTo(new[] { a0, i2, i3});
    }

    [Fact]
    public void AutoRefreshWithObservablePredicate1()
    {
        var item1 = new ActivableItem
        {
            Activated = false
        };

        var items = new SourceList<ActivableItem>();
        items.Add(item1);

        var filterSubject = new BehaviorSubject<Func<ActivableItem, bool>>(_ => false);

        var obsListDerived = items
            .Connect()
            .AutoRefresh(i => i.Activated)
            .Filter(filterSubject)
            .Do(x => Console.WriteLine("Changes" + x.TotalChanges))
            .AsObservableList();

        // Default filter predicate denies all items
        // The binding collection should stay empty, until the predicate changes
        obsListDerived.Count.Should().Be(0);

        item1.Activated = true;
        obsListDerived.Count.Should().Be(0);

        item1.Activated = false;
        obsListDerived.Count.Should().Be(0);

        item1.Activated = true;
        obsListDerived.Count.Should().Be(0);

        // Changing predicate, all "Activated" items should added to the binding collection
        filterSubject.OnNext(i => i.Activated);

        obsListDerived.Count.Should().Be(1);
        obsListDerived.Items.Should().BeEquivalentTo(new[] { item1 });

        // Changing property value
        item1.Activated = false;
        obsListDerived.Count.Should().Be(0);
    }

    [Fact]
    public void AutoRefreshWithObservablePredicate2()
    {
        var item1 = new ActivableItem
        {
            Activated = false
        };

        var items = new ObservableCollection<ActivableItem>();
        items.Add(item1);

        var filterSubject = new BehaviorSubject<Func<ActivableItem, bool>>(_ => false);

        var obsListDerived = items
            .ToObservableChangeSet()
            .AutoRefresh(i => i.Activated)
            .Filter(filterSubject)
            .AsObservableList();

        // Default filter predicate denies all items
        // The binding collection should stay empty, until the predicate changes
        obsListDerived.Count.Should().Be(0);

        item1.Activated = true;
        obsListDerived.Count.Should().Be(0);

        item1.Activated = false;
        obsListDerived.Count.Should().Be(0);

        item1.Activated = true;
        obsListDerived.Count.Should().Be(0);

        // Changing predicate, all "Activated" items should added to the binding collection
        filterSubject.OnNext(i => i.Activated);

        obsListDerived.Count.Should().Be(1);
        obsListDerived.Items.Should().BeEquivalentTo(new[] { item1 });

        // Changing property value multiple times
        item1.Activated = false;
        item1.Activated = true;
        item1.Activated = false;
        item1.Activated = true;
        obsListDerived.Count.Should().Be(1);
        obsListDerived.Items.Should().BeEquivalentTo(new[] { item1 });
    }
}

public class Item(string name) : INotifyPropertyChanged
{
    private string _name = name;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
}

public class ActivableItem : INotifyPropertyChanged
{
    private bool _activated;

    public bool Activated
    {
        get => _activated;
        set
        {
            _activated = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Activated)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
