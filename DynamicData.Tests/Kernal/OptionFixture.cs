using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    public class OptionFixture
    {

        [Test]
        public void OptionSomeHasValue()
        {
            var person = new Person("Name", 20);
            var option = Optional.Some(person);
            Assert.IsTrue(option.HasValue, "HasValue should be true");
            Assert.IsTrue(ReferenceEquals(person, option.Value), "Shuld be same person");
       }

        [Test]
        public void ImplictCastHasValue()
        {
            var person = new Person("Name", 20);
            Optional<Person> option = person;

            Assert.IsTrue(option.HasValue, "HasValue should be true");
            Assert.IsTrue(ReferenceEquals(person, option.Value), "Shuld be same person");
        }

        [Test]
        public void OptionSetToNullHasNoValue1()
        {
            Person person = null;
            var option = Optional.Some(person);
            Assert.IsFalse(option.HasValue, "HasValue should be false");
       }
        [Test]
        public void OptionSetToNullHasNoValue2()
        {
            Person person = null;
            Optional<Person> option = person;
            Assert.IsFalse(option.HasValue, "HasValue should be false");
        }

        [Test]
        public void OptionNoneHasNoValue()
        {
            var option = Optional.None<IChangeSet<Person, string>>();
            Assert.IsFalse(option.HasValue, "HasValue should be false");
        }


    }
}
