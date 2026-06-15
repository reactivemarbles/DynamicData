using System;
using System.Reactive;
using System.Reactive.Subjects;

class Program {
    static void Main() {
        var s = new AsyncSubject<Unit>();
        s.OnNext(Unit.Default);
        s.OnCompleted();
        s.Dispose();
        
        try {
            s.Subscribe(
                _ => Console.WriteLine("Next"),
                ex => Console.WriteLine("Error: " + ex.GetType()),
                () => Console.WriteLine("Completed")
            );
            Console.WriteLine("Subscribed successfully (this shouldn't happen)");
        } catch (Exception ex) {
            Console.WriteLine("Outer Error: " + ex.GetType());
        }
    }
}
