using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core
{
    public record StudentReport(
        int Id,
        string Name,
        int GroupId,
        double AttendancePercent);
}
