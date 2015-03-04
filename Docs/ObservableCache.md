# Observable Cache

The observable cache lies at the heart of dynamic data as it is the main data store and acts as the root observable change set .  It  exposes direct data access methods as well as observables to stream data and any changes to data.  Since it is a cache it requires a key which is specified when constructed.

The methods exposed are as follows

```csharp
    public interface IObservableCache<TObject, TKey>
    {
	    //The observable methods
	    IObservable<IChangeSet<TObject, TKey>> Connect();
        IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter);
        IObservable<Change<TObject, TKey>> Watch(TKey key);
        IObservable<int> CountChanged { get; }
		  
		//The direct data methods
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TObject> Items { get; }
        IEnumerable<KeyValuePair<TKey,TObject>> KeyValues { get; }
        Optional<TObject> Lookup(TKey key);
        int Count { get; }
    }
```
There are essentially 2 main flavours of observable cache which are 

 1. ```SourceCache<TObject,TKey>``` which is the read write version
 2. ```AnomynousObservableCache<TObject,TKey>``` which is the read only version

As a developer you will only ever directly work with the source cache as the read only version is constructed by invoking the ```AsObservableCache()``` extension method. This call hides the write methods of the source cache which is advisable if the cache is to be exposed outside the scope in which it is created i.e. to the rest of the system.

To construct it you would need to specify a key
```csharp
var mycache  = new SourceCache<TObject,TKey>(t => t.Key);
```
but if you wish to use the object's hash code there is an overloaded version which uses an integer key.
```csharp
var mycache  = new SourceCache<TObject>();
```
There is actually a third cache called ```IntermediateCache<TObject,TKey>``` which is identical to to the source cache but does not accepts a key selector. This is mainly used when a cache is needed within a custom operator and there is no way of knowing what the key is.

## Amending data

The source cache effectively has one method which is:
```csharp
 public interface ISourceCache<TObject, TKey> : IObservableCache<TObject, TKey>
 {
    void BatchUpdate(Action<ISourceUpdater<TObject, TKey>> updateAction);
 }
``` 
At a glance this may look a little odd, but let me explain first. ```ISourceUpdater``` exposes methods to update and query the inner cache where one call to it produces a single change set notification.  The additional benefit is many changes to the cache can be produced with a single lock to the data in the cache.  

```csharp
   myCache.BatchUpdate(updater =>
   {
       updater.Clear();
       updater.AddOrUpdate(myListOfItems);
   });
``` 
In this example the cache is locked once and produces a single change set to reflect the cache being cleared and loaded with 'myListOfItems'. However since this method is awkward, there is a load of convenience extensions which invoke the  batch update method for you. For example could do the following
```csharp
myCache.Clear();
myCache.AddOrUpdate(myListOfItems);
```
which results in 2 locks to the cache and 2 change set notifications. Less efficient but easier code.


## The data access  methods

Sometimes is is necessary to access data without subscribing to an observable. This is why the observable cache has direct data access methods. 
```csharp
IEnumerable<TKey> Keys { get; }
IEnumerable<TObject> Items { get; }
IEnumerable<KeyValuePair<TKey,TObject>> KeyValues { get; }
Optional<TObject> Lookup(TKey key);
int Count { get; }
```
Not much to be said about them as I would say they are mostly self explanatory so I will be brief.  These methods are thread safe as they are accessed within the cache's internal lock.  Lookup is the equivalent of a TryGet() but avoids the ugly semantic of passing an out variable. It returns an ```Optional<TObject>``` which is the equivalent of a nullable type but can be used both for value and reference types.

## The observable methods

These methods are used to observe changes to the data in the cache.  Whenever any of the methods are subscribed to they will notify listeners of any data changes, preceded by any relevant  data which is already contained in the cache at the moment of subscription.
 
```csharp
IObservable<IChangeSet<TObject, TKey>> Connect();
IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter);
IObservable<Change<TObject, TKey>> Watch(TKey key);
IObservable<int> CountChanged { get; }
```
### Connnect

Creates an observable change set which is the core interface of dynamic data. Almost all dynamic data extensions are build upon ```IObservable<IChangeSet<TObject, TKey>>```. If the overload accepting the filter is used, only matching items are included in subsequent notifications.

```csharp
 var myObservableChangeSet = myCache.Connect();
 //or
 var myObservableChangeSet = myCache.Connect(x=>//return a predicate);
``` 
This is the most widely used method on the cache

### Watch

Watch enables the observation of a single item matching the specified key.

### CountChanged

Simple lightweight count observable starting with the count upon subscription.