using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UChatClientConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string user = Console.ReadLine();
            Console.Write(user.Length);
            Console.ReadKey();
            /*string password = Console.ReadLine();
            UChatClientConsole u = new UChatClientConsole(user, password);
            u.StartClient();
            u.Login();
            while(true)
            {
                string tmp = Console.ReadLine();
                u.SendMsg(tmp);
            }*/
        }
    }
}
