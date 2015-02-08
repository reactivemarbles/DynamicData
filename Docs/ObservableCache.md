# Observable Cache

The observable cache is the key data store in dynamic data. It stores data using a key which is specified when constructed.  It is the core component of dynamic data as it exposes direct data access methods as well as observables to stream data and any changes to data it contains.

The methods exposed are as follows

```csharp
    public interface IObservableCache<TObject, TKey>
    {
	    //The observable methods
        IObservable<Change<TObject, TKey>> Watch(TKey key);
        IObservable<IChangeSet<TObject, TKey>> Connect();
        IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> filter);
        IObservable<int> CountChanged { get; }
		  
		//The direct data methods
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TObject> Items { get; }
        IEnumerable<KeyValuePair<TKey,TObject>> KeyValues { get; }
        Optional<TObject> Lookup(TKey key);
        int Count { get; }
    }
```
The are essentially 2 flavours of observable cache which are 

 1. ```SourceCache<TObject,TKey>``` which is the read write version
 2. ```AnomynousObservableCache<TObject,TKey>``` which is the read only version

As a developer you will only ever directly work with the source cache as the read only version is constructed by invoking the ```AsObservableCache()``` extension method. This call hides the write methods of the source cache which is advisable if the cache is to be exposed outside the scope in which it is created i.e. to the rest of the system.

To construct it you would need to specify a key
```csharp
var mycache  = new SourceCache<TObject,TKey>(t => t.Key);
```
but if you wish to use the object's hash code there is an overloaded version which uses an integer key

```csharp
var mycache  = new SourceCache<TObject>();
```
The source cache effectively has one method which is:
```csharp
 public interface ISourceCache<TObject, TKey> : IObservableCache<TObject, TKey>
  {
	void BatchUpdate(Action<ISourceUpdater<TObject, TKey>> updateAction);
  }
``` 
At a glance this may look a little odd, but let me explain first. ```ISourceUpdater``` exposes methods to update and query the inner cache where one call to it produces a single change set notification.  The benefit is many changes to the cache can be produced with single lock to the data in the cache.  

```csharp
   myCache.BatchUpdate(updater =>
   {
       updater.Clear();
       updater.AddOrUpdate(myListOfItems);
   });
``` 
In this example the cache is locked once and produces a single change set to reflect the cache being cleared and loaded with 'myListOfItems'. However since this method is awkward, there a a load of convenience extensions which invoke batch update for you. For example could do the following
```csharp
myCache.Clear();
myCache.AddOrUpdate(myListOfItems);
```
which results in 2 locks to the cache and 2 change set notifications. Less efficient but easier code.



