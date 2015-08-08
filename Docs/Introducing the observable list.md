### Introducing the observable list

Create a source list like this.
```csharp
var myObservableList = new SourceList<T>()
```
Now you can connect to it using ```myObservableList.Connect()``` which creates an observable change set meaning that whenever there is an add, update, delete or move to ```myObservableList``` a notification is transmitted. 

From here you can start composing sophisticated observations. For example if my list was a list of trades I can do this
```csharp
var mySubscription = myObservableList.Connect() 
					.Filter(t=>trade.Status == TradeStatus.Live) 
					.Transform(trade => new TradeProxy(trade)) //equivalent to rx .Select
					.Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
					.DisposeMany()
					....//do something with the result
```
where the source is filtered to include live trades only, transformed into a proxy, ordered by time and the proxy is disposed of when removed from the underlying filtered result.  As the list is edited the result set always reflects the changes made in the source list. Imagine how much plumbing would be required to maintain a collection to do all that yet dynamic data does it effectively in one line of code.

If you are familiar with using dynamic data's observable cache you will recognise these operators. This is intentional as  I have tried to replicate the observable cache operators. So far I have created about 25 operators for the observable list and in time will try and replicate most if not all of the cache operators.

Editing the source  list is easy as the list has the usual add / insert / remove methods.  For a batch edit I have provided a method which enables batch editing as follows
```csharp
_source.Edit(innerList =>
{
    innerList.Clear();
    innerList.AddRange(myItemsToAdd);
});
```
This method will clear and load the source list yet produce a single notification which helps improve efficiency.

The source list is thread-safe and can be shared but before sharing I  recommend you call ```myObservableList.AsObservableList()``` which hides the edit methods.  Additionally you can call ```.AsObservableList()``` on any observable change set. So for example if you want to share a filtered observable list you can do this.

```csharp
IObservableList<T> filteredObservableList = myObservableList.Connect()  
					.Filter(t=>trade.Status == TradeStatus.Live) 
					.AsObservableList();		
```
which is a self-maintaining filtered observable list.  I hope you think that is cool! 
