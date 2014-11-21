using System;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using NUnit.Framework;

namespace DynamicData.Tests.Operators
{
    [TestFixture]
    public class TransformManyFixture
    {
        private ISourceCache<PersonWithRelations, string> _source;
        private IObservable<IChangeSet<PersonWithRelations, string>> _stream;

        [SetUp]
        public void Initialise()
        {
            // _stream = ObservableCacheEx.For<PersonWithRelations, string>(p => p.Name).GetFeeder(f => _source = f);
            _source = new SourceCache<PersonWithRelations, string>(p=>p.Key);
        }

        [Test]
        public void Test1()
        {
            var frientofchild1 = new PersonWithRelations("Friend1", 10);
            var child1 = new PersonWithRelations("Child1", 10, new[] {frientofchild1});
            var child2 = new PersonWithRelations("Child2", 8);
            var child3 = new PersonWithRelations("Child3", 8);
            var mother = new PersonWithRelations("Mother", 35, new[] {child1, child2, child3});
            var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

            var disposable = _source.Connect().TransformMany(p => p.Relations.RecursiveSelect(r => r.Relations),p => p.Name)
                                            .IgnoreUpdateWhen((current, previous) => current.Name == previous.Name)
                                            .Subscribe(x => { Console.WriteLine(); });

            _source.BatchUpdate(updater =>
                {
                    updater.AddOrUpdate(mother);
                    updater.AddOrUpdate(father);
                });

            disposable.Dispose();
        }

       


    }
}