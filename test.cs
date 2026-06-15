using System;
using System.Reactive.Linq;
using DynamicData;

class Program {
    static void Main() {
        var list = new SourceList<int>();
        list.Connect().Subscribe(changes => {
            foreach (var change in changes) {
                if (change.Reason == ListChangeReason.Clear) {
                    Console.WriteLine($"Clear range count: {change.Range.Count}");
                }
            }
        });
        list.Add(1);
        list.Add(2);
        list.Clear();
    }
}
