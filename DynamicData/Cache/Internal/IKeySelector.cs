
namespace DynamicData.Cache.Internal
{
    internal interface IKeySelector<in TObject, out TKey> //: IKeySelector<TObject>
    {
        TKey GetKey(TObject item);
    }
}
