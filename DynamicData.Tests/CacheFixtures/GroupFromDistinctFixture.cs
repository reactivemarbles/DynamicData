using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class GroupFromDistinctFixture
    {
          
        private ISourceCache<Person, string>  _personCache;
        private ISourceCache<PersonEmployment, PersonEmpKey> _employmentCache;

        [SetUp]
        public void SetStream()
        {
            _personCache = new SourceCache<Person, string>(p=>p.Key);
            _employmentCache = new SourceCache<PersonEmployment, PersonEmpKey>(e=>e.Key);
        }


        [TearDown]
        public void CleanUp()
        {
            _personCache?.Dispose();
            _employmentCache?.Dispose();
        }


        [Test]
        public void GroupFromDistinct()
        {
            const int numberOfPeople = 1000;
            var random = new Random();
            var companies = new List<string> { "Company A", "Company B", "Company C" };

            //create 100 people
            var people = Enumerable.Range(1, numberOfPeople).Select(i => new Person("Person{0}".FormatWith(i), i)).ToArray();

            //create 0-3 jobs for each person and select from companies
            var emphistory = Enumerable.Range(1, numberOfPeople).SelectMany(i =>
                                                                                {
                                                                                    var companiestogenrate = random.Next(0, 4);
                                                                                    return Enumerable.Range(0, companiestogenrate).Select(c => new PersonEmployment("Person{0}".FormatWith(i), companies[c]));
                                                                                }).ToList();

            // Cache results
            var allpeopleWithEmpHistory = _employmentCache.Connect()
                .Group(e => e.Name, _personCache.Connect().DistinctValues(p => p.Name))
                .Transform(x => new PersonWithEmployment(x))
                .AsObservableCache();


            _personCache.AddOrUpdate(people);
            _employmentCache.AddOrUpdate(emphistory);

            Assert.AreEqual(numberOfPeople, allpeopleWithEmpHistory.Count);
            Assert.AreEqual(emphistory.Count, allpeopleWithEmpHistory.Items.SelectMany(d => d.EmpoymentData.Items).Count());

            //check grouped items have the same key as the parent
            allpeopleWithEmpHistory.Items.ForEach
                (p =>
                     {
                         Assert.IsTrue(p.EmpoymentData.Items.All(emph => emph.Name == p.Person));
                     }

                );
            
            _personCache.Edit(updater => updater.Remove("Person1"));
            Assert.AreEqual(numberOfPeople - 1, allpeopleWithEmpHistory.Count);
            _employmentCache.Edit(updater => updater.Remove(emphistory));
            allpeopleWithEmpHistory.Dispose();
        }


        
    }
}