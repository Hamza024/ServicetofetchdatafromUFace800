using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPushAttendanceService
{
    public class DeviceResponseVM
    {
        public int idwIndex { get; set; }
        public int sdwEnrollNumber { get; set; }
        public string sdwEmployeeName { get; set; }
        public int idwVerifyMode { get; set; }
        public int idwInOutMode { get; set; }
        public DateTime idwDate { get; set; }
        public int idwWorkcode { get; set; }
    }
}
