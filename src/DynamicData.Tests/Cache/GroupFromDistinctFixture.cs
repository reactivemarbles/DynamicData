using System;
using System.Collections.Generic;
using System.Linq;

using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class GroupFromDistinctFixture : IDisposable
{
    private readonly ISourceCache<PersonEmployment, PersonEmpKey> _employmentCache;

    private readonly ISourceCache<Person, string> _personCache;

    public GroupFromDistinctFixture()
    {
        _personCache = new SourceCache<Person, string>(p => p.Key);
        _employmentCache = new SourceCache<PersonEmployment, PersonEmpKey>(e => e.Key);
    }

    public void Dispose()
    {
        _personCache?.Dispose();
        _employmentCache?.Dispose();
    }

    [Fact]
    public void GroupFromDistinct()
    {
        const int numberOfPeople = 1000;
        var random = new Random();
        var companies = new List<string> { "Company A", "Company B", "Company C" };

        //create 100 people
        var people = Enumerable.Range(1, numberOfPeople).Select(i => new Person($"Person{i}", i)).ToArray();

        //create 0-3 jobs for each person and select from companies
        var emphistory = Enumerable.Range(1, numberOfPeople).SelectMany(
            i =>
            {
                var companiestogenrate = random.Next(0, 4);
                return Enumerable.Range(0, companiestogenrate).Select(c => new PersonEmployment($"Person{i}", companies[c]));
            }).ToList();

        // Cache results
        var allpeopleWithEmpHistory = _employmentCache.Connect().Group(e => e.Name, _personCache.Connect().DistinctValues(p => p.Name)).Transform(x => new PersonWithEmployment(x)).AsObservableCache();

        _personCache.AddOrUpdate(people);
        _employmentCache.AddOrUpdate(emphistory);

        allpeopleWithEmpHistory.Count.Should().Be(numberOfPeople);
        allpeopleWithEmpHistory.Items.SelectMany(d => d.EmploymentData.Items).Count().Should().Be(emphistory.Count);

        //check grouped items have the same key as the parent
        allpeopleWithEmpHistory.Items.ForEach(p => { p.EmploymentData.Items.All(emph => emph.Name == p.Person).Should().BeTrue(); });

        _personCache.Edit(updater => updater.Remove("Person1"));
        allpeopleWithEmpHistory.Count.Should().Be(numberOfPeople - 1);
        _employmentCache.Edit(updater => updater.Remove(emphistory));
        allpeopleWithEmpHistory.Dispose();
    }
}
