using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.Tests.External
{

    public interface IFruit
    {
        
    }

    class Apple : IFruit { }
    class Banana : IFruit { }
    class Mango : IFruit { }





    class OrExperiment
    {


        public void Test()
        {
            var apples = new SourceList<Apple>();
            var bananas = new SourceList<Banana>();
            var mangos = new SourceList<Mango>();




            var items = new SourceList<IObservable<IChangeSet<IFruit>>>();
            items.Add(apples.Cast(item => (IFruit) item));
            items.Add(bananas.Cast(item => (IFruit) item));
            items.Add(mangos.Cast(item => (IFruit) item));

            var result = items.Or().AsObservableList();

            //var fruits = ObservableList.Or<IFruit>(apples, bananas, mangos).AsObservableList();

            //// Does something similar to this exist?
            //var fruits = ObservableList.Or<IFruit>(apples, bananas, mangos).AsObservableList();
        }
    }

    public static class ObservableList
    {
        public static IObservable<IChangeSet<TDestination>> Or<TSource, TDestination>(params IObservableList<TSource>[] sources)
            where TSource:TDestination
        {
            
        }
    }
}
