using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Cache;
using DynamicData.Kernel;
using NUnit.Framework;

namespace DynamicData.Tests.External
{
    class Student
    {
        public int Id { get; }
        public string Name { get;}

        public Student(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    class Class
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public IList<int> StudentIds { get; private set; }

        public Class(int id, string name, IList<int> studentIds)
        {
            Id = id;
            Name = name;
            StudentIds = studentIds;
        }
    }

    class Grade
    {
        public int StudentId { get;  }
        public int ClassId { get;  }
        public int Value { get;}

        public Grade(int studentId, int classId, int value)
        {
            StudentId = studentId;
            ClassId = classId;
            Value = value;
        }
    }


    class StudentSummary
    {
        public Class[] Classes { get;  }
        public Student Student { get;  }
        public int[] Grades { get; private set; }

        public StudentSummary(Student student, IGrouping<StudentIdWithClass, Tuple<int, Class>, int> classes, int[] grades)
        {
            Student = student;
            Classes = classes.Items.Select(x=>x.Class).AsArray();
            Grades = grades;
        }
    }


    class StudentIdWithClass
    {
        public Tuple<int, Class> Key { get; }
        public int StudentId { get; }
        public Class Class { get; }

        public StudentIdWithClass(int studentId, Class @class)
        {
            Key = Tuple.Create(studentId, @class);
            StudentId = studentId;
            Class = @class;
        }
    }

    [TestFixture]
    public class StudentTest
    {
        [Test]
        public void Test()
        {
            var students = new SourceCache<Student, int>(student => student.Id);
            var classes = new SourceCache<Class, int>(@class => @class.Id);
            var grades = new SourceList<Grade>();

            var alice = new Student(1, "Alice");
            students.AddOrUpdate(alice);
            var bob = new Student(2, "Bob");
            students.AddOrUpdate(bob);

            var math = new Class(1, "Math", new List<int> { alice.Id });
            classes.AddOrUpdate(math);
            var biology = new Class(2, "Biology", new List<int> { alice.Id, bob.Id });
            classes.AddOrUpdate(biology);
            var algorithms = new Class(3, "Algorithms", new List<int> { bob.Id });
            classes.AddOrUpdate(algorithms);

            grades.Add(new Grade(alice.Id, math.Id, 5));
            grades.Add(new Grade(bob.Id, biology.Id, 4));
            grades.Add(new Grade(bob.Id, algorithms.Id, 2));

            
            var studentsByClass = classes.Connect()
                .TransformMany(@class => @class.StudentIds.Select(studentId => new StudentIdWithClass(studentId, @class)), x => x.Key);

            var studentsSummary = students.Connect()
                .InnerJoinMany(studentsByClass, swc => swc.StudentId, (studentId, student, grouping) => new StudentSummary(student, grouping,new int[0]))
                .AsObservableCache();

            //    Console.WriteLine(String.Join(", ", studentsSummary.Lookup(alice.Id).Value.Classes));
            algorithms.StudentIds.Add(alice.Id);
            classes.AddOrUpdate(algorithms);
        //    Console.WriteLine(String.Join(", ", studentsSummary.Lookup(alice.Id).Value.Classes));
        }


    }
}
