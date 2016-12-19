using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{
    [TestFixture]
    public class ExpressionBuilderFixture
    {
        [Test]
        public void GenerateExpression()
        {
            var instance =new ClassA();
            
            var members1 = ExpressionBuilder.GetMembers<ClassA, string>(a => a.Name).ToArray();
            var members2 = ExpressionBuilder.GetMembers<ClassA, int>(a => a.Child.Age).ToArray();

            var xxxx = members2
                .Select(m => m.GetProperty())
                .ToArray();

            //require the following
            //1) Lambda to get value.
            //2) Check property changes for each parent object i.e. is a.Child INPC

           // xxxx.Select(p => p.MemberType)

        }

        public class ClassA: AbstractNotifyPropertyChanged
        {
            private string _name;

            public string Name
            {
                get { return _name; }
                set { SetAndRaise(ref _name, value); }
            }

            private ClassB _classB;

            public ClassB Child
            {
                get { return _classB; }
                set { SetAndRaise(ref _classB, value); }
            }
        }

        public class ClassB : AbstractNotifyPropertyChanged
        {
            private int _age;

            public int Age
            {
                get { return _age; }
                set { SetAndRaise(ref _age, value); }
            }
        }

    }
}
