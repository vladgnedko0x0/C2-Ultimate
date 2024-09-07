using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace C2_Ultimate.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string MACAddress { get; set; }
        public string Password { get; set; }
        public string SecretKey { get; set; }
        public bool isAdmin {  get; set; }
        public override string ToString()
        {
            return $"Id: {Id}, Name: {Name}, MACAddress: {MACAddress}, Password: {Password}";
        }
    }
}
