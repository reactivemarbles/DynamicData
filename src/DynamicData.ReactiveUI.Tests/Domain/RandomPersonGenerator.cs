using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.ReactiveUI.Tests.Domain
{
    public class RandomPersonGenerator
    {
        readonly IEnumerable<string> _boys = new List<string>()
        {
            "Sergio","Daniel","Carolina","David","Reina","Saul","Bernard","Danny",
            "Dimas","Yuri","Ivan","Laura","John","Bob","Charles","Rupert","William",
            "Englebert","Aaron","Quasimodo","Henry","Edward","Zak",
            "Kai", "Dominguez","Escobar","Martin","Crespo","Xavier","Lyons","Stephens","Aaron"
        };

        private readonly IEnumerable<string> _girls = new List<string>()
        {
            "Ruth","Katy","Patricia","Nikki","Zoe","Esmerelda","Fiona","Amber","Kirsty","Zaira",
            "Claire","Isabel","Esmerelda","Nicola","Lucy","Louise","Elizabeth","Anne","Rebecca",
            "Rhian","Beatrice"
        };

        private readonly IEnumerable<string> _lastnames = new List<string>()
        {
            "Johnson","Williams","Jones","Brown","David","Miller","Wilson","Anderson","Thomas",
            "Jackson","White","Robinson","Williams","Jones","Windor","McQueen","X", "Black",
            "Green","Chicken","Partrige","Broad","Flintoff","Root"
        };

        private readonly Random _random = new Random();
        public IEnumerable<Person> Take(int number = 10000)
        {

            var girls = (from first in _girls
                from second in _lastnames
                from third in _lastnames
                select new { First = first, Second = second, Third = third, Gender = "F" });

            var boys = (from first in _boys
                from second in _lastnames
                from third in _lastnames
                select new { First = first, Second = second, Third = third, Gender = "M" });


            var maxage = 100;
            return girls.Union(boys).AsParallel().OrderBy(x => Guid.NewGuid())
                .Select
                (x =>
                {
                    var lastname = x.Second == x.Third ? x.Second : string.Format("{0}-{1}", x.Second, x.Third);
                    var age = _random.Next(0, maxage);
                    return new Person(x.First, lastname, age, x.Gender);
                }
                )
                .Take(number).ToList();

        }

    }
}