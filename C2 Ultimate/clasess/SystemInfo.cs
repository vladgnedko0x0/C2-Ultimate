using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2_Ultimate.clasess
{
    public class SystemInfo
    {
        public string Type { get; set; } // "connect", "client", "server"
        public string MachineName { get; set; }
        public string OSVersion { get; set; }
        public int ProcessorCount { get; set; }
        public string MacAddress { get; set; }
        public string TotalVisibleMemory { get; set; }
        public string FreePhysicalMemory { get; set; }
        public string TotalVirtualMemory { get; set; }
        public string FreeVirtualMemory { get; set; }
        public List<string>? SecretKeys { get; set; }
        public string? SecretKey { get; set; }
    }
}

