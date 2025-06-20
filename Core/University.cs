using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class University
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<Institute> Institutes { get; set; }
    }
    public class Institute
    {
        public string Name { get; set; }
        public List<Department> Departments { get; set; } = new();
    }

    public class Department
    {
        public string Name { get; set; }
        public List<string> Specializations { get; set; } = new();
    }
}
