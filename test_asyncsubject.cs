using System;
using System.Reactive;
using System.Reactive.Subjects;

class Program
{
    static void Main()
    {
        var subject = new AsyncSubject<Unit>();
        subject.OnNext(Unit.Default);
        subject.OnCompleted();
        subject.Dispose();
        
        try {
            subject.Subscribe(x => Console.WriteLine("Next"), () => Console.WriteLine("Completed"));
            Console.WriteLine("Success");
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.GetType().Name);
        }
    }
}
