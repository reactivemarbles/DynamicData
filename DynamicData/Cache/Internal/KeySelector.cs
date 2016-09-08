using System;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal
{
    internal sealed class KeySelector<TObject, TKey> : IKeySelector<TObject, TKey>
    {
        private readonly Func<TObject, TKey> _keySelector;

        public KeySelector(Func<TObject, TKey> keySelector)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            _keySelector = keySelector;
        }

        public Type Type => typeof(TObject);

        public TKey GetKey(TObject item)
        {
            try
            {
                return _keySelector(item);
            }
            catch (Exception ex)
            {
                throw new KeySelectorException("Error returning key", ex);
            }
        }
    }
}
