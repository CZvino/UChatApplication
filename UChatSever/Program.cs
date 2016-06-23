using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UChatServer
{
    class Program
    {
        static void Main(string[] args)
        {
            UChatServer myServer = new UChatServer();

            myServer.StartServer();

            while (true)
            {
                
            }
        }
    }
}
