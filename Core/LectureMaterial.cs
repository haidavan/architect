using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public class LectureMaterial
    {
        public int LectureId { get; set; }
        public string LectureName { get; set; }
        public string CourseName { get; set; }
        public string Content { get; set; }
        public List<string> Keywords { get; set; } = new();
        public string FilePath { get; set; }
    }
}
