using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    public class RightJoinManyFixture
    {
        private SourceCache<Person, string> _people;
        private ChangeSetAggregator<ParentAndChildren, string> _result;

        [SetUp]
        public void Initialise()
        {
            _people = new SourceCache<Person, string>(p => p.Name);
            //All children will be included whether there is a parent or not
            _result = _people.Connect()
                .RightJoinMany(_people.Connect(), pac => pac.ParentName, (personid, person, grouping) => new ParentAndChildren(personid, person, grouping.Items.Select(p => p).ToArray()))
                .AsAggregator();
        }


        [TearDown]
        public void CleanUp()
        {
            _people.Dispose();
            _result.Dispose();
        }


        [Test]
        public void AddLeftOnly()
        {
            var people = Enumerable.Range(1, 10)
                .Select(i => new Person("Person" + i, i))
                .ToArray();

            _people.AddOrUpdate(people);

            Assert.AreEqual(1, _result.Data.Count);
            Assert.IsNull(_result.Data.Items.First().Parent);
        }


        [Test]
        public void AddPeopleWithParents()
        {
            var people = Enumerable.Range(1, 10)
                .Select(i =>
                {
                    string parent = "Person" + CalculateParent(i, 10);
                    return new Person("Person" + i, i, parentName: parent);
                })
                .ToArray();

            _people.AddOrUpdate(people);

            AssertDataIsCorrectlyFormed(people);
        }

        [Test]
        public void UpdateParent()
        {
            var people = Enumerable.Range(1, 10)
                .Select(i =>
                {
                    string parent = "Person" + CalculateParent(i, 10);
                    return new Person("Person" + i, i, parentName: parent);
                })
                .ToArray();

            _people.AddOrUpdate(people);

            var current10 = people.Last();
            var person10 = new Person("Person10", 100, parentName: current10.ParentName);
            _people.AddOrUpdate(person10);

            var updatedPeople = people.Take(9).Union(new[] { person10 }).ToArray();

            AssertDataIsCorrectlyFormed(updatedPeople);
        }

        [Test]
        public void UpdateChild()
        {
            var people = Enumerable.Range(1, 10)
                .Select(i =>
                {
                    string parent = "Person" + CalculateParent(i, 10);
                    return new Person("Person" + i, i, parentName: parent);
                })
                .ToArray();

            _people.AddOrUpdate(people);

            var current6 = people[5];
            var person6 = new Person("Person6", 100, parentName: current6.ParentName);
            _people.AddOrUpdate(person6);

            var updatedPeople = people.Where(p => p.Name != "person6").Union(new[] { person6 }).ToArray();

            AssertDataIsCorrectlyFormed(updatedPeople);
        }

        [Test]
        public void AddChild()
        {
            var people = Enumerable.Range(1, 10)
                .Select(i =>
                {
                    string parent = "Person" + CalculateParent(i, 10);
                    return new Person("Person" + i, i, parentName: parent);
                })
                .ToArray();

            _people.AddOrUpdate(people);

            var person11 = new Person("Person11", 100, parentName: "Person3");
            _people.AddOrUpdate(person11);

            var updatedPeople = people.Union(new[] { person11 }).ToArray();

            AssertDataIsCorrectlyFormed(updatedPeople);
        }


        [Test]
        public void RemoveChild()
        {
            var people = Enumerable.Range(1, 10)
                .Select(i =>
                {
                    string parent = "Person" + CalculateParent(i, 10);
                    return new Person("Person" + i, i, parentName: parent);
                })
                .ToArray();

            _people.AddOrUpdate(people);

            var last = people.Last();
            _people.Remove(last);

            var updatedPeople = people.Where(p => p.Name != last.Name).ToArray();

            AssertDataIsCorrectlyFormed(updatedPeople, last.Name);
        }


        private void AssertDataIsCorrectlyFormed(Person[] allPeople, params string[] missingParents)
        {
            var grouped = allPeople.GroupBy(p => p.ParentName)
                .Where(p => p.Any() && !missingParents.Contains(p.Key))
                .AsArray();


            Assert.AreEqual(grouped.Length, _result.Data.Count);

            grouped.ForEach(grouping =>
            {
                if (missingParents.Length > 0 && missingParents.Contains(grouping.Key)) return;

                var result = _result.Data.Lookup(grouping.Key)
                    .ValueOrThrow(() => new Exception("Missing result for " + grouping.Key));

                var children = result.Children;
                CollectionAssert.AreEquivalent(grouping, children);
            });
        }



        private int CalculateParent(int index, int totalPeople)
        {
            if (index < 5)
                return 11;

            if (index == totalPeople - 1)
                return 1;

            if (index == totalPeople)
                return 1;

            return index + 1;
        }

    }
}