using System;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
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
            option.HasValue.Should().BeTrue();
            ReferenceEquals(person, option.Value).Should().BeTrue();
        }

        [Test]
        public void ImplictCastHasValue()
        {
            var person = new Person("Name", 20);
            Optional<Person> option = person;

            option.HasValue.Should().BeTrue();
            ReferenceEquals(person, option.Value).Should().BeTrue();
        }

        [Test]
        public void OptionSetToNullHasNoValue1()
        {
            Person person = null;
            var option = Optional.Some(person);
            option.HasValue.Should().BeFalse();
        }

        [Test]
        public void OptionSetToNullHasNoValue2()
        {
            Person person = null;
            Optional<Person> option = person;
            option.HasValue.Should().BeFalse();
        }

        [Test]
        public void OptionNoneHasNoValue()
        {
            var option = Optional.None<IChangeSet<Person, string>>();
            option.HasValue.Should().BeFalse();
        }

        [Test]
        public void OptionIfHasValueInvokedIfOptionHasValue()
        {
            Optional<Person> source = new Person("A", 1);

            bool ifactioninvoked = false;
            bool elseactioninvoked = false;

            source.IfHasValue(p => ifactioninvoked = true)
                .Else(() => elseactioninvoked = true);

            ifactioninvoked.Should().BeTrue();
            elseactioninvoked.Should().BeFalse();
        }

        [Test]
        public void OptionElseInvokedIfOptionHasNoValue()
        {
            Optional<Person> source = null;

            bool ifactioninvoked = false;
            bool elseactioninvoked = false;

            source.IfHasValue(p => ifactioninvoked = true)
                .Else(() => elseactioninvoked = true);

            ifactioninvoked.Should().BeFalse();
            elseactioninvoked.Should().BeTrue();
        }
    }
}
