
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

var subject = new Subject<int>();
var sub = subject.Buffer(TimeSpan.FromSeconds(0.1)).Subscribe(
    b => Console.WriteLine("Batch"),
    () => Console.WriteLine("Completed")
);
subject.Dispose();
Thread.Sleep(500);
Console.WriteLine("Done");

