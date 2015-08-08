## Dynamic Data

Dynamic Data is a portable class library which brings the power of Reactive Extensions (Rx) to collections.  

Mutable collections frequently experience additions, updates, and removals (among other changes). Dynamic Data provides two collection implementations, `ISourceCache<T>` and `ISourceList<T>`, that expose changes to the collection via an observable change set. The resulting observable change sets can be manipulated and transformed using Dynamic Data's robust and powerful array of change set operators. These operators receive change notifications, apply some logic, and subsequently provide their own change notifications. Because of this, operators are fully composable and can be chained together to perform powerful and very complicated operations while maintaining simple, fluent code.

Using Dynamic Data's collections and change set operators makes in-memory data management extremely easy and can reduce the size and complexity of your code base by abstracting complicated and often repetitive operations.

###Some links

- [![Join the chat at https://gitter.im/RolandPheasant/DynamicData](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/RolandPheasant/DynamicData?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
- [![Downloads](https://img.shields.io/nuget/dt/DynamicData.svg)](http://www.nuget.org/packages/DynamicData/)	
- [![Build status]( https://ci.appveyor.com/api/projects/status/occtlji3iwinami5/branch/develop?svg=true)](https://ci.appveyor.com/project/RolandPheasant/DynamicData/branch/develop)
- Sample wpf project https://github.com/RolandPheasant/Dynamic.Trader
- Blog at  http://dynamic-data.org/
- You can contact me on twitter  [@RolandPheasant](https://twitter.com/RolandPheasant) or email at [roland@dynamic-data.org]

### Version 4 has been released

The core of Dynamic Data is the observable cache, which is great in most circumstances. However, sometimes the simplicity of an observable list is needed and version 4 delivers this. The addition of the observable list has been a great effort and has consumed loads of my time, mental capacity, and resolve. In spite of the difficulty, the observable list is finally crystallising into a stable state. 

Downloading the latest release of Dynamic Data from [Dynamic Data on nuget](https://www.nuget.org/packages/DynamicData/) will allow you to create and have fun with observable lists.

## Create Dynamic Data Collections

### The Observable List

Create an observable list like this:
```cs
var myInts = new SourceList<int>();
```
The observable list provides the direct edit methods you would expect. For example:
```cs
myInts.AddRange(Enumerable.Range(0, 10000)); 
myInts.Add(99999); 
myInts.Remove(99999);
```
Each of the amendments caused by the AddRange operation above will produce a unique change notification. When making multiple modifications, batch editing a list using the `.Edit` operator is much more efficient and produces only a single change notification.
```cs
myInts.Edit(innerList =>
{
   innerList.Clear();
   innerList.AddRange(Enumerable.Range(0, 10000));
});
```
If ``myInts`` is to be exposed publicly it can be made read only using `.AsObservableList`
```cs
IObservableList<int> readonlyInts = myInts.AsObservableList();
```
which hides the edit methods.

The list's changes can be observed by calling `myInts.Connect()` like this:
```cs
IObservable<IChangeSet<int>> myIntsObservable = myInts.Connect();
```
This creates an observable change set for which there are dozens of operators. The changes are transmitted as an Rx observable, so they are fluent and composable.

### The observable cache

Create an observable cache like this:
```cs
var myCache = new SourceCache<TObject,TKey>(t => key);
```
There are direct edit methods, for example

```cs
myCache.Clear();
myCache.AddOrUpdate(myItems);
```
Each amend operation will produced a change notification. A much more efficient option is to batch edit which produces a single notification.

```cs
myCache.BatchUpdate(innerCache =>
			  {
			      innerCache.Clear();
			      innerCache.AddOrUpdate(myItems);
			  });
```
If ```myCache``` is to be exposed publicly it can be made read only

```cs
IObservableCache<TObject,TKey> readonlyCache= myCache.AsObservableCache();
```
which hides the edit methods.

The cache is observed by calling ```myInts.Connect()```. This creates an observable change set for which there are dozens of cache specific operators. The changes are transmitted as an Rx observable so are fluent and composable.

## Consume Dynamic Data

### Connect to the cache or the list

As stated in the blurb at the top of this document, Dynamic Data is based on the concept of an observable change set. Calling the ```Connect()``` on the list or the cache will produce an observable change set. 

```cs
var myConnection = myDynamicDataSource.Connect();
```
This opens the consumer to fluent and composable streams of data. But before I show some examples, there are some alternative ways to create an observable change set.

### Create an observable change set from a standard Rx observable

Given either of the following observables
```cs
IObservable<T> myObservable;
IObservable<IEnumerable<T>> myObservable;
```
an observable cache can be created like like 
```cs
var myConnection = myObservable.ToObservableChangeSet(t=> t.key);
```

### Create a size or time based expiring cache

The problem with the above is the cache will grow forever so there are overloads to specify size limitation or expiry times. The following shows how to limit the size and create a time expiring cache.

Expire by time
```cs
var myConnection = myObservable.ToObservableChangeSet(t=> t.key, expireAfter: item => TimeSpan.FromHours(1));
```
where the expiry time for each item can be specified. Alternatively expire by size
```cs
var myConnection = myObservable.ToObservableChangeSet(t=> t.key, limitSizeTo:10000);
```
There is also an overload to expire by both time and size.

### Create an observable change set from an observable collection

Another way is to create an observable change set from an observable collection.
```cs
var myObservableCollection = new ObservableCollection<T>();
```
To create a cache observable specify a key
```cs
var myConnection = myObservableCollection.ToObservableChangeSet(t => t.Key);
```
or to create a list observable
```cs
var myConnection = myObservableCollection.ToObservableChangeSet();
```
This method is only recommended for simple queries which act only on the UI thread as ```ObservableCollection``` is not thread safe.

## Some powerful examples

No you can create an observable cache or an observable list, here are a few quick fire examples to illustrated the diverse range of things you can do. In all of these examples the resulting sequences always exactly reflect the items is the cache i.e. adds, updates and removes are always propagated.

#### Bind to a complex stream

This example a stream of live trades, creates a proxy for each trade and order the result by most recent first. The result is bound to the observable collection. (```ObservableCollectionExtended<T>``` is provided by dynamic data and is more efficient than the standard ``ObservableCollection<T>``` )
```cs
//Dynamic Data has it's own take on an observable collection (optimised for populating f
var list = new ObservableCollectionExtended<TradeProxy>();
var myoperation = myConnection 
					.Filter(trade=>trade.Status == TradeStatus.Live) 
					.Transform(trade => new TradeProxy(trade))
					.Sort(SortExpressionComparer<TradeProxy>.Descending(t => t.Timestamp))
					.ObserveOnDispatcher()
					.Bind(list) 
					.DisposeMany()
					.Subscribe()
```
Oh and I forgot to say, ```TradeProxy``` is disposable and ```DisposeMany()``` ensures items are disposed when no longer part of the stream.

#### Create a derived list or cache

Although this example is very simple, it is one of the most powerful aspects of Dynamic Data.  Any Dynamic Data stream can be materialised into a derived collection.  

If you have 
```cs
var myList = new SourceList<People>()
```
You can do this
```cs
var oldPeople = myList.Filter(person => person.Age > 65).AsObservableList();
```
and you have an observable list of pensioners.

The same applies to a cache.  The only difference is you call ```.AsObservableCache()``` to create a derived cache.

In practise I have found this function very useful in a trading system where old items massively outnumber current items.  By creating a derived collection and exposing that to consumers has saved a huge amount of processing power and memory consumption.

#### Filtering
Filter the underlying data using the filter operators
```cs
var myOperation = personChangeSet.Filter(person => person.Age > 50) 
```
or to dynamically change a filter 
```cs
IObservable<Func<Person,bool>> observablePredicate=...;
var myOperation = personChangeSet.Filter(observablePredicate) 
```
#### Sorting

Filter the underlying data using the filter operators
```cs
var myOperation = personChangeSet.Sort(SortExpressionComparer.Ascending(p => p.Age) 
```
or to dynamically change a sort
```cs
IObservable<IComparer<Person>> observableComparer=...;
var myoperation = personChangeSet.Sort(observableComparer) 
```
#### Grouping

This operator pre-caches the specified groups according to the group selector.
```cs
var myOperation = personChangeSet.GroupOn(person => person.Status)
```

#### Transformation

Map to a another object
```cs
var myOperation = personChangeSet.Transform(person => new PersonProxy(person)) 
```
Ceate a fully formed reactive tree
```cs
var myOperation = personChangeSet.TransformToTree(person => person.BossId) 
```
Flatten  a child enumerable
```cs
var myOperation = personChangeSet.TransformMany(person => person.Children) 
```
#### Aggregation

if we have a a list of people we can aggregate as follows
```cs
var count= 	personChangeSet.Count();
var max= 	personChangeSet.Max(p => p.Age);
var min= 	personChangeSet.Min(p => p.Age);
var stdDev= personChangeSet.StdDev(p => p.Age);
var avg= 	personChangeSet.Avg(p => p.Age);
```
In the near future I will create even more aggregations.

#### Join operators

There are And, Or, Xor and Except logical operators
```cs
var peopleA = new SourceCache<Person,string>(p => p.Name);
var peopleB = new SourceCache<Person,string>(p => p.Name);

var observableA = peopleA.Connect();
var observableB = peopleB.Connect();

var inBoth = observableA.And(observableB);
var inEither= observableA.Or(observableB);
var inOnlyOne= observableA.Xor(observableB);
var inAandNotinB = observableA.Except(observableB);
```
Currently the join operators are only implemented for cache observables

#### Disposal handler

To ensure an object is disposed when it is removed from a stream
```cs
var myOperation = somedynamicdatasource.Connect().DisposeMany()
```
which will also dispose all objects when the stream is disposed. This is typically used when a transform function creates an object which is disposable.

#### Distinct Values

```DistinctValues()``` will produce an observable of distinct changes in the underlying collection.
```cs
var people = personSource.DistinctValues(trade => trade.Age)
```
In this case a distinct ages.


#### Virtualisation

Visualise data to restrict by index and segment size
```cs
IObservable<IVirtualRequest> request; //request stream
var virtualisedStream = someDynamicDataSource.Virtualise(request)
```
Visualise data to restrict by index and page size
```cs
IObservable<IPageRequest> request; //request stream
var pagedStream = someDynamicDataSource.Page(request)
```
In either of the above, the result is re-evaluated when the request stream changes

Top is an overload of ```Virtualise()``` and will return items matching the first 'n'  items.
```cs
var topStream = someDynamicDataSource.Top(10)
```
#### Observing binding changes

If the collection has objects which implement ```INotifyPropertyChanged``` the the following operators are available
```cs
var ageChanged = peopleDataSource.WhenValueChanged(p => p.Age)
```
which returns an observable of the age when the value of Age has changes, .
```cs
var ageChanged = peopleDataSource.WhenPropertyChanged(p => p.Age)
```
which returns an observable of the person and age when the value of Age has changes, .
```cs
var personChanged = peopleDataSource.WhenAnyPropertyChanged()
```
which returns an observable of the person when any property has changed,.

#### Observing item changes

Binding is a very small part of Dynamic Data. The above notify property changed overloads are just an example when binding. If you have a domain object which has children observables you can use ```MergeMany()``` which subscribes to and unsubscribes from items according to collection changes.

```cs
var myoperation = somedynamicdatasource.Connect() 
			.MergeMany(trade => trade.SomeObservable());
```
This wires and unwires ```SomeObservable``` as the collection changes.

## History of Dynamic Data
Even before Rx existed I had implemented a similar concept using old fashioned events but the code was very ugly and my implementation full of race conditions so it never existed outside of my own private sphere. My second attempt was a similar implementation to the first but using Rx when it first came out. This also failed as my understanding of Rx was flawed and limited and my design forced consumers to implement interfaces.  Then finally I got my design head on and in 2011-ish I started writing what has become dynamic data. No inheritance, no interfaces, just the ability to plug in and use it as you please.  All along I meant to open source it but having so utterly failed on my first 2 attempts I decided to wait until the exact design had settled down. The wait lasted longer than I expected and end up taking over 2 years but the benefit is it has been trialled for 2 years on a very busy high volume low latency trading system which has seriously complicated data management. And what's more that system has gathered a load of attention for how slick and cool and reliable it is both from the user and IT point of view. So I present this library with the confidence of it being tried, tested, optimised and mature. I hope it can make your life easier like it has done for me.

## Want to know more?
I could go on endlessly but this is not the place for full documentation.  I promise this will come but for now I suggest downloading my WPF sample app (links at top of document)  as I intend it to be a 'living document' and I promise it will be continually maintained. 

Also if you following me on Twitter you will find out when new samples or blog posts have been updated.

Additionally if you have read up to here and not pressed star then why not? Ha. A star may make me be more responsive to any requests or queries.

