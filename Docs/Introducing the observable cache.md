### Introducing the observable cache

Normally a cache is constructed using a key selector.
```csharp
var myObservableCache= new SourceCache<TObject,TKey>(t => key);
```
but if you want the hash code to be used as a key you can construct it without specifying the key
```csharp
var myObservableCache= new SourceCache<TObject>();
```
Now you can connect to the cache using ```myObservableCache.Connect()``` which creates an observable change set meaning that whenever there is change to ```myObservableCache``` a notification is transmitted. 

Exactly like the observable list you can now start composing sophisticated observations. For example if the cache is a cache of trades you can do this
```csharp
var mySubscription = myObservableCache.Connect()  
					.Filter(t=>trade.Status == TradeStatus.Live) 
					.Transform(trade => new TradeProxy(trade)) //equivalent to rx .Select
					.Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
					.DisposeMany()
					....//do something with the result
```
where the source is filtered to include live trades only, transformed into a proxy, ordered by time and the proxy is disposed of when removed from the underlying filtered result.  As the list is edited the result set always reflects the changes made in the source list. Imagine how much plumbing would be required to maintain a collection to do all that yet dynamic data does it effectively in one line of code.

Editing the source  cache is easy as it has the usual add / update / remove methods.  For a batch edit I have provided a method which enables batch editing as follows
```csharp
myObservableCache.BatchUpdate(innerCache =>
				  {
				      innerCache.Clear();
				      innerCache.AddOrUpdate(myItems);
				  });
```
This method will clear and load the source list yet produce a single notification which helps improve efficiency.

The cache is thread-safe and can be shared but before sharing I  recommend you call ```myObservableCache.AsObservableCache()``` which hides the edit methods.  Additionally you can call ```.AsObservableCache()``` on any observable change set. So for example if you want to share a filtered observable cache you can do this.

```csharp
IObservableCache<T> filteredObservableCache = myObservableCache.Connect() 
					.Filter(t=>trade.Status == TradeStatus.Live) 
					.AsObservableCache();		
```
which is a self-maintaining filtered observable cache. 