using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace DynamicData.Binding
{
    /// <summary>
    /// Template class for INotifyPropertyChanged
    /// </summary>
    public abstract class NotifyPropertyChangedBase: INotifyPropertyChanged
    {
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string property)
        {
            if (this.PropertyChanged == null)
            {
                return;
            }

            Action action = () => this.PropertyChanged(this, new PropertyChangedEventArgs(property));
            action();
        }


        /// <summary>
        /// Fires PropertyChanged event
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression">The expression.</param>
        protected void OnPropertyChanged<T>(Expression<Func<T>> expression)
        {
            this.OnPropertyChanged(this.GetProperty(expression).Name);
        }

        private PropertyInfo GetProperty<T>(Expression<Func<T>> expression)
        {
            var property = this.GetMember(expression) as PropertyInfo;
            if (property == null)
            {
                throw new ArgumentException("Not a property expression", this.GetMember(() => expression).Name);
            }

            return property;
        }

        private MemberInfo GetMember<T>(Expression<Func<T>> expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(this.GetMember(() => expression).Name);
            }

            return this.GetMemberInfo(expression);
        }

        private MemberInfo GetMemberInfo(LambdaExpression lambda)
        {
            if (lambda == null)
            {
                throw new ArgumentNullException(this.GetMember(() => lambda).Name);
            }

            MemberExpression memberExpression = null;
            if (lambda.Body.NodeType == ExpressionType.Convert)
            {
                memberExpression = ((UnaryExpression)lambda.Body).Operand as MemberExpression;
            }
            else if (lambda.Body.NodeType == ExpressionType.MemberAccess)
            {
                memberExpression = lambda.Body as MemberExpression;
            }
            else if (lambda.Body.NodeType == ExpressionType.Call)
            {
                return ((MethodCallExpression)lambda.Body).Method;
            }

            if (memberExpression == null)
            {
                throw new ArgumentException("Not a member access", this.GetMember(() => lambda).Name);
            }

            return memberExpression.Member;
        }
    }
}