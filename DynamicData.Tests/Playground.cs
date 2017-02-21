using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Kernel;
using DynamicData.List.Internal;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests
{
    public class Playground
    {
        private readonly Random _random = new Random();

        [Test]
        public async Task Test1()
        {
            var tasks = Enumerable.Range(1, 100000).Select(CreateRandomTask);

            var result = await tasks.SelectParallel(20);
            
            var xxx = result.Select(r => r.ThreadId).Distinct().ToList();
            Console.WriteLine(result);
        }

        private Task<Result> CreateRandomTask(int number)
        {
            Task.Delay(_random.Next(1, 25));
            return Task.FromResult(new Result(number));
        }

        private class Result
        {
            public int ThreadId { get; }
            public int Number { get; }

            public Result(int number)
            {
                Number = number;
                ThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            public override string ToString()
            {
                return $"{nameof(ThreadId)}: {ThreadId}, {nameof(Number)}: {Number}";
            }
        }


        [Test]
        public void Cast()
        {
            //take any list
            var people = new SourceList<Person>();

            //convert to dynamic
            dynamic dyn = people;

            //do a little magic
            IObservable<IChangeSet<object>> objectChanges = Converter.AsObjectList(dyn);
            IObservableList<object> myList = objectChanges.AsObservableList();
        }
    }

    public static class Converter
    {
        public static IObservable<IChangeSet<object>> AsObjectList<T>(this SourceList<T> source)
        {
            return source.Connect().Select(changes =>
            {
                var items = changes.Transform(t => (object)t);
                return new ChangeSet<object>(items);
            });
        }
    }

}
