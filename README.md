## Dynamic Data

Bring the power of Rx to collections using Dynamic Data. 

### What is it?

A comprehensive library of reactive extensions, which are used to manage in-memory collections. As the source collection changes the operators receive a changeset which enables them to self-maintain.

### Why use it?

It makes the management of in memory-data easy and is no exageration to say it can save thousands of lines of code.

### Go on then show me

First create a source of data:

```csharp
//use  SourceList<T> to compare items on hash code otherwise use SourceCache<TObject,TKey>.
var mySource = new SourceList<Trade>();
```
The following snippet connects to a stream of live trades, creates a proxy for each trade and orders the results by most recent first. As the source is modified the result of ‘myoperation’ will automatically reflect changes.

```csharp
//some code to maintain the source (not shown)
var myoperation = mySource.Connect() 
            .Filter(trade=>trade.Status == TradeStatus.Live) 
            .Transform(trade => new TradeProxy(trade))
            .Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
            .DisposeMany()
             //more operations...
            .Subscribe(changeSet=>//do something with the result)
```
Oh and I forgot to say, ```TradeProxy``` is disposable and DisposeMany() ensures items are disposed when no longer part of the stream.

The following example produces a stream which is grouped by status. In this case, an observable cache is created for each unique status.  When there are no items in a group the group is removed.

```csharp
var myoperation = mySource.Connect() 
            .Group(trade=>trade.Status) 
			.Subscribe(changeSet=>//do something with the result)
```

There are a myriad of operators and 

### Ok I'll give it a go

Install from Nuget:  https://www.nuget.org/packages/DynamicData
Sample wpf project: https://github.com/RolandPheasant/TradingDemo 
Blog: http://dynamicdataproject.wordpress.com
Feel free to feedback on twitter:  [@RolandPheasant](https://twitter.com/RolandPheasant)









