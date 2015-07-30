using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DynamicData.Tests
{
    internal static class RandomStuff
    {
        public static IObservable<T> SwitchFirst<T>(this IObservable<IObservable<T>> source)
        {
            return source.Take(1).Switch().Repeat();
        }

        public static IObservable<T> SwitchFirst2<T>(this IObservable<IObservable<T>> source)
        {
            return source.Publish(shared => shared.Take(1).Switch().Repeat());

        }

        public static IObservable<TSource> SwitchFirstSafe3<TSource>(this IObservable<IObservable<TSource>> source)
        {
            return Observable.Create<TSource>(o => {
                int free = 1;
                return source.Where(_ =>
                    Interlocked.CompareExchange(ref free, 0, 1) == 1
                ).Select(el => el.Finally(() => free = 1)).Switch().Subscribe(o);
            }
            );
        }
    }

    [TestFixture]
    public class SwitchFirstFixture
    {



        [Test]
        [Explicit]
        public void TakeUntilComplete()
        {
            var combined = new Subject<IObservable<string>>();


            // var subjects = new List<Subject<string>>();
            Func<string, ISubject<string>> add = s =>
            {
                var subject = new ReplaySubject<string>(1);

                combined.OnNext(subject);
                subject.OnNext(s);
                return subject;
            };


            var sub = combined.SwitchFirst().Subscribe(Console.WriteLine);


            var a = add("a");
            var b = add("b");
            var c = add("c");
            var d = add("d");

            a.OnCompleted();

            var e = add("e");
            e.OnCompleted();
            var f = add("f");
        }
    }
}
