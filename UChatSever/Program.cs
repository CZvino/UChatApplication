using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UChatServer
{
    public class UChatServer
    {
        #region ----------定义变量----------
        private const string ipAddr = "127.0.0.1";  //监听ip
        private const int port = 3000;              //监听port

        private Thread threadWatch = null;          //负责监听客户端请求的线程
        private Socket socketWatch = null;          //负责监听服务端的嵌套字

        //保存了服务器端所有负责调用通信套接字的Receive方法的线程
        private Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();
        //保存了所有客户端通信的套接字和客户端的用户名
        private Dictionary<string, Socket> dictSocket = new Dictionary<string, Socket>();
        //保存了服务器端所有和客户端通信的套接字
        private Dictionary<string, string> dictUser = new Dictionary<string, string>();
        #endregion

        public UChatServer()
        {

        }

        #region ----------开启服务器----------
        /// <summary>
        ///     开启服务器
        /// </summary>
        /// <returns>返回开启服务器的成功性</returns>
        public bool StartServer()
        {
            //创建负责监听的套接字，参数使用IPv4寻址协议，使用流式连接，使用TCP协议传输数据
            socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //创建一个绑定IP和port的网络节点对象
            IPAddress ipAddress = IPAddress.Parse(ipAddr);
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);

            //将监听socket绑定到创建的网络节点
            try
            {
                socketWatch.Bind(endPoint);

                //设置监听等待队列的长度
                socketWatch.Listen(20);

                //创建负责监听的西安城，并传入监听方法
                threadWatch = new Thread(WatchConnection);
                threadWatch.IsBackground = true;
                threadWatch.Start();

                Console.WriteLine();
                Console.WriteLine("                            ---服务器已成功启动监听---");
                Console.WriteLine();

                return true;
            }
            catch (SocketException es)
            {
                Console.WriteLine("【错误】" + es.Message);
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("【错误】" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void WatchConnection()
        {

        }
        #endregion
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("                            ---服务器已成功启动监听---");
            Console.WriteLine();
        }
    }
}
