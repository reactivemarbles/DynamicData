using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
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
        //By Dorus on Gitter
        public static IObservable<TSource> DoFirst<TSource>(this IObservable<TSource> source, int n, Action<TSource> action)
        {
            return Observable.Create<TSource>(o => {
                var pub = source.Publish();
                return new CompositeDisposable(
                    pub.Take(n).Subscribe(action),
                    pub.Subscribe(o),
                    pub.Connect());
            }
            );
        }

        public static IObservable<TSource> DoFirst<TSource>(this IObservable<TSource> source,  Action<TSource> action)
        {
            return source.Publish(shared => shared.Take(1).Do(action).Concat(shared));
        }

        //By Dorus on Gitter
        public static IObservable<TSource> Amb<TSource>(this IObservable<IObservable<TSource>> source)
        {
            return Observable.Create<TSource>(o => {
                int first = -1;
                return source.TakeWhile(_ => first == -1)
                    .Select((el, c) => el
                        .DoFirst(1, _ => Interlocked.CompareExchange(ref first, c, -1))
                        .TakeWhile(_ => first == c))
                        .Merge().Subscribe(o);
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
