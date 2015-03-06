using DynamicData.Tests.Domain;
using NUnit.Framework;
using Console = System.Console;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class TestCollection
    {
        readonly RandomPersonGenerator _random = new RandomPersonGenerator();

        [Test]
        public void CanAccumulate()
        {
            var items = new ObservableList<Person>();


            var subscription = items.Changes
                .Subscribe(changes =>
                {
                    Console.WriteLine(changes);
                });


            using (var x = items.SuspendNotifications())
            {
                foreach (var person in _random.Take(20000))
                {
                    items.Add(person);
                }
                items.Clear();
                var result = items.GetChanges();

                items[10] = new Person("Roland", 1);
            }

        }
    }
}