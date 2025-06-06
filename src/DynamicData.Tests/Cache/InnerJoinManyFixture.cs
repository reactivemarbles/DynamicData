using System;
using System.Linq;

using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class InnerJoinManyFixture : IDisposable
{
    private readonly SourceCache<Person, string> _people;

    private readonly ChangeSetAggregator<ParentAndChildren, string> _result;

    public InnerJoinManyFixture()
    {
        _people = new SourceCache<Person, string>(p => p.Name);
        //Only parent which have children will be included
        _result = _people.Connect().InnerJoinMany(_people.Connect(), pac => pac.ParentName, (person, grouping) => new ParentAndChildren(person, grouping.Items.Select(p => p).ToArray())).AsAggregator();
    }

    [Fact]
    public void AddChild()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var person11 = new Person("Person11", 100, parentName: "Person3");
        _people.AddOrUpdate(person11);

        var updatedPeople = people.Union(new[] { person11 }).ToArray();

        AssertDataIsCorrectlyFormed(updatedPeople);
    }

    [Fact]
    public void AddLeftOnly()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Person" + i, i)).ToArray();

        _people.AddOrUpdate(people);

        _result.Data.Count.Should().Be(0);
    }

    [Fact]
    public void AddPeopleWithParents()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        AssertDataIsCorrectlyFormed(people);
    }

    public void Dispose()
    {
        _people.Dispose();
        _result.Dispose();
    }

    [Fact]
    public void RefreshRightKey()
    {
        _people.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Person() { Name = "Person #1", ParentName = string.Empty } );
                innerCache.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" } );
                innerCache.AddOrUpdate(new Person() { Name = "Person #3", ParentName = "Person #2" } );
            });

        var refreshPerson = _people.Lookup("Person #2").Value;


        // Change pairing
        refreshPerson.ParentName = "Person #3";
        _people.Refresh(refreshPerson);

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().NotContain(("Person #1", "Person #2"));
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().Contain(("Person #3", "Person #2"));


        // Remove pairing
        refreshPerson.ParentName = "Person #4";
        _people.Refresh(refreshPerson);

        _result.Data.Count.Should().Be(1);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (parentName: family.Parent.Name, chilldName: child.Name))).Should().NotContain(pair => pair.chilldName == "Person #2");


        // Restore pairing
        refreshPerson.ParentName = "Person #1";
        _people.Refresh(refreshPerson);

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().Contain(("Person #1", "Person #2"));


        // No change
        _people.Refresh(refreshPerson);

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().Contain(("Person #1", "Person #2"));
    }

    [Fact]
    public void RemoveChild()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var last = people.Last();
        _people.Remove(last);

        var updatedPeople = people.Where(p => p.Name != last.Name).ToArray();

        AssertDataIsCorrectlyFormed(updatedPeople, last.Name);
    }

    [Fact]
    public void UpdateChild()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var current6 = people[5];
        var person6 = new Person("Person6", 100, parentName: current6.ParentName);
        _people.AddOrUpdate(person6);

        var updatedPeople = people.Where(p => p.Name != "Person6").Union(new[] { person6 }).ToArray();

        AssertDataIsCorrectlyFormed(updatedPeople);
    }

    [Fact]
    public void UpdateParent()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var current10 = people.Last();
        var person10 = new Person("Person10", 100, parentName: current10.ParentName);
        _people.AddOrUpdate(person10);

        var updatedPeople = people.Take(9).Union(new[] { person10 }).ToArray();

        AssertDataIsCorrectlyFormed(updatedPeople);
    }

    [Fact]
    public void UpdateRightKey()
    {
        _people.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Person() { Name = "Person #1", ParentName = string.Empty } );
                innerCache.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" } );
                innerCache.AddOrUpdate(new Person() { Name = "Person #3", ParentName = "Person #2" } );
            });

        
        // Change pairing
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #3" });

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().NotContain(("Person #1", "Person #2"));
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().Contain(("Person #3", "Person #2"));


        // Remove pairing
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #4" });

        _result.Data.Count.Should().Be(1);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (parentName: family.Parent.Name, chilldName: child.Name))).Should().NotContain(pair => pair.chilldName == "Person #2");


        // Restore pairing
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" });

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().Contain(("Person #1", "Person #2"));


        // No change
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" });

        _result.Data.Count.Should().Be(2);
        _result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name))).Should().Contain(("Person #1", "Person #2"));
    }

    private void AssertDataIsCorrectlyFormed(Person[] allPeople, params string[] missingParents)
    {
        var grouped = allPeople.GroupBy(p => p.ParentName).Where(p => p.Any() && !missingParents.Contains(p.Key)).AsArray();

        _result.Data.Count.Should().Be(grouped.Length);

        grouped.ForEach(
            grouping =>
            {
                if (missingParents.Length > 0 && missingParents.Contains(grouping.Key))
                {
                    return;
                }

                var result = _result.Data.Lookup(grouping.Key).ValueOrThrow(() => new Exception("Missing result for " + grouping.Key));

                var children = result.Children;
                children.Should().BeEquivalentTo(grouping);
            });
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private int CalculateParent(int index, int totalPeople)
    {
        if (index < 5)
        {
            return 10;
        }

        if (index == totalPeople - 1)
        {
            return 1;
        }

        if (index == totalPeople)
        {
            return 1;
        }

        return index + 1;
    }
}
