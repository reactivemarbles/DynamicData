using System.Collections.Generic;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.Kernal
{
    [TestFixture]
    public class KeyValueFixture
    {
        [Test]
        public void Create()
        {
            var person = new Person("Person", 10);
            var kv = new KeyValuePair<string,Person>("Person", person);

            Assert.AreEqual("Person", kv.Key);
            Assert.AreEqual(person, kv.Value);
        }
    }
}