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
          
        private ISourceCache<Person, string>  _personFeeder;
        private ISourceCache<PersonEmployment, PersonEmpKey> _employmentFeeder;

        [SetUp]
        public void SetStream()
        {
            _personFeeder = new SourceCache<Person, string>(p=>p.Key);
            _employmentFeeder = new SourceCache<PersonEmployment, PersonEmpKey>(e=>e.Key);
        }


        [TearDown]
        public void CleanUp()
        {
            if (_personFeeder != null)
                _personFeeder.Dispose();

            if (_employmentFeeder != null)
                _employmentFeeder.Dispose();
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
            var allpeopleWithEmpHistory = _employmentFeeder.Connect()
                .Group(e => e.Name, _personFeeder.Connect().DistinctValues(p => p.Name))
                .Transform(x => new PersonWithEmployment(x))
                .AsObservableCache();


            _personFeeder.BatchUpdate(updater => updater.AddOrUpdate(people));
            _employmentFeeder.BatchUpdate(updater => updater.AddOrUpdate(emphistory));

            Assert.AreEqual(numberOfPeople, allpeopleWithEmpHistory.Count);
            Assert.AreEqual(emphistory.Count, allpeopleWithEmpHistory.Items.SelectMany(d => d.EmpoymentData.Items).Count());

            //check grouped items have the same key as the parent
            allpeopleWithEmpHistory.Items.ForEach
                (p =>
                     {
                         Assert.IsTrue(p.EmpoymentData.Items.All(emph => emph.Name == p.Person));
                     }

                );
            
            _personFeeder.BatchUpdate(updater => updater.Remove("Person1"));
            Assert.AreEqual(numberOfPeople - 1, allpeopleWithEmpHistory.Count);
            _employmentFeeder.BatchUpdate(updater => updater.Remove(emphistory));
            allpeopleWithEmpHistory.Dispose();
        }


        
    }
}