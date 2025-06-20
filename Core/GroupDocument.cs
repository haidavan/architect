using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class GroupDocument
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public List<Student> Students { get; set; }
    }
}
