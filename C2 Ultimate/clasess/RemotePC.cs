using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace C2_Ultimate.clasess
{
    internal class RemotePC
    {
        public TcpClient client;
        //Server its target client its Attacker
        public bool isClientOrServer;
        public List<string> secretKeys;
       public SystemInfo info;
        public NetworkStream stream;

        public RemotePC (TcpClient client,bool isClientOrServer,SystemInfo info,NetworkStream stream, List<string> secretKeys)
        {
            this.client= client;
            this.isClientOrServer= isClientOrServer;
            this.secretKeys= secretKeys;
            this.info= info;
            this.stream= stream;
        }
    }
}
