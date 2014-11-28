using System;

namespace DynamicData.Kernel
{
    public class OptionElse
    {

        internal readonly static OptionElse NoAction = new OptionElse(false);

        private readonly bool _shouldRunAction;

        internal OptionElse(bool shouldRunAction=true)
        {
            _shouldRunAction = shouldRunAction;
        }

        /// <summary>
        /// Invokes the specified action when an option has no value.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <exception cref="System.ArgumentNullException">action</exception>
        public void Else(Action action)
        {
            if (action == null) throw new ArgumentNullException("action");
            if (_shouldRunAction) action();

        }
    }
}