using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture(Ignore = true, IgnoreReason = "Temporarily made obsolete")]
    public class UniqueChangeSetFixture
    {
        [Test]
        public void AddAndRemoveOnlyProducesRemove()
        {
            var updates = new List<Change<Person, string>>
                              {
                                  new Change<Person, string>(ChangeReason.Add, "P1", new Person("Person1",20)),
                                  new Change<Person, string>(ChangeReason.Remove, "P1", new Person("Person1",20)),
                              };


            var unique = new UniqueChangeSet<Person, string>(updates);
            Assert.AreEqual(1,unique.Count);
            Assert.AreEqual(ChangeReason.Remove, unique.First().Reason);
        }

        [Test]
        public void AddAndUpdateOnlyProducesUpdate()
        {
            var initial = new Person("Person1", 20);
            var updates = new List<Change<Person, string>>
                              {
                                  new Change<Person, string>(ChangeReason.Add, "P1", initial),
                                  new Change<Person, string>(ChangeReason.Update, "P1", new Person("Person1",21),initial),
                              };


            var unique = new UniqueChangeSet<Person, string>(updates);
            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(ChangeReason.Update, unique.First().Reason);
            Assert.AreEqual(21, unique.First().Current.Age);
            Assert.AreEqual(20, unique.First().Previous.Value.Age);
        }

        [Test]
        public void MultipleUpdatesProducesLastUpdate()
        {
            var updates = new List<Change<Person, string>>
                              {
                                  new Change<Person, string>(ChangeReason.Add,    "P1", new Person("Person1", 20)),
                                  new Change<Person, string>(ChangeReason.Update, "P1", new Person("Person1",21),new Person("Person1", 20)),
                                  new Change<Person, string>(ChangeReason.Update, "P1", new Person("Person1",22),new Person("Person1", 21)),
                                  new Change<Person, string>(ChangeReason.Update, "P1", new Person("Person1",23),new Person("Person1", 22)),
                              };


            var unique = new UniqueChangeSet<Person, string>(updates);
            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(ChangeReason.Update, unique.First().Reason);
            Assert.AreEqual(23, unique.First().Current.Age);
            Assert.AreEqual(22, unique.First().Previous.Value.Age);
        }
        
        [Test]
        public void EvaluateDoesNotSupercedeAdd()
        {
            var updates = new List<Change<Person, string>>
                              {
                                  new Change<Person, string>(ChangeReason.Add, "P1", new Person("Person1",20)),
                                  new Change<Person, string>(ChangeReason.Evaluate, "P1", new Person("Person1",20)),
                              };


            var unique = new UniqueChangeSet<Person, string>(updates);
            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(ChangeReason.Add, unique.First().Reason);
        }
       
        [Test]
        public void EvaluateDoesNotSupercedeUpdate()
        {
            var updates = new List<Change<Person, string>>
                              {
                                  new Change<Person, string>(ChangeReason.Update, "P1", new Person("Person1",21),new Person("Person1",20)),
                                  new Change<Person, string>(ChangeReason.Evaluate, "P1", new Person("Person1",21)),
                              };


            var unique = new UniqueChangeSet<Person, string>(updates);
            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(ChangeReason.Update, unique.First().Reason);
        }

        [Test]
        public void EvaluateDoesNotSupercedeRemove()
        {
            var updates = new List<Change<Person, string>>
                              {
                                  new Change<Person, string>(ChangeReason.Remove, "P1", new Person("Person1",20)),
                                  new Change<Person, string>(ChangeReason.Evaluate, "P1", new Person("Person1",20)),
                              };


            var unique = new UniqueChangeSet<Person, string>(updates);
            Assert.AreEqual(1, unique.Count);
            Assert.AreEqual(ChangeReason.Remove, unique.First().Reason);
        }

    }
}