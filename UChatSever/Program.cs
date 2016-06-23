using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace UChatServer
{
    public class UChatServer
    {
        #region ----------     定义变量     ----------
        private const byte LOGIN = 0;
        private const byte SENDTOINDIVIDULAL = 1;
        private const byte SENDTOALL = 2;

        private const string ipAddr = "127.0.0.1";  //监听ip
        private const int port = 3000;              //监听port

        private Thread threadWatch = null;          //负责监听客户端请求的线程
        private Socket socketWatch = null;          //负责监听服务端的嵌套字

        //保存了服务器端所有负责调用通信套接字的Receive方法的线程
        private Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();
        //保存了所有客户端通信的套接字和客户端的用户名
        private Dictionary<string, Socket> dictSocket = new Dictionary<string, Socket>();
        //保存了服务器端所有和客户端通信的套接字
        private Dictionary<string, string> dictRequestLoginUser = new Dictionary<string, string>();
        private Dictionary<string, string> dictOnlineUser = new Dictionary<string, string>();
        private Dictionary<string, string> dictOnlineUserO = new Dictionary<string, string>();
        #endregion

        public UChatServer()
        {

        }

        #region ----------    开启服务器    ----------
        /// <summary>
        ///     开启服务器
        /// </summary>
        public void StartServer()
        {
            //创建负责监听的套接字，参数使用IPv4寻址协议，使用流式连接，使用TCP协议传输数据
            socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //创建一个绑定IP和port的网络节点对象
            IPAddress ipAddress = IPAddress.Parse(ipAddr);
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);
           
            try
            {
                //将监听socket绑定到创建的网络节点
                socketWatch.Bind(endPoint);

                //设置监听等待队列的长度
                socketWatch.Listen(20);

                //创建负责监听的线程，并传入监听方法
                threadWatch = new Thread(WatchConnection);
                //设置为后台线程
                threadWatch.IsBackground = true;
                //开启线程
                threadWatch.Start();

                Console.WriteLine();
                Console.WriteLine("                          ---服务器已成功启动监听---");
                Console.WriteLine();

                return;
            }
            catch (SocketException se)
            {
                Console.WriteLine("【错误】" + se.Message);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("【错误】" + e.Message);
                return;
            }
        }
        #endregion

        #region ----------  监听客户端请求  ----------
        /// <summary>
        ///     监听客户端请求
        /// </summary>
        private void WatchConnection()
        {
            //持续监听请求
            while(true)
            {
                Socket socketConnection = null;
                try
                {
                    //监听请求，如果有新请求则返回一个socket
                    socketConnection = socketWatch.Accept();
                    string socketKey = socketConnection.RemoteEndPoint.ToString();
                    
                    //判断当前用户是否之前已请求并且成功登陆
                    if (dictRequestLoginUser.ContainsKey(socketKey) == true)
                    {
                        //将用户从请求登陆队列移到在线队列
                        dictOnlineUser.Add(socketKey, dictRequestLoginUser[socketKey]);
                        dictOnlineUserO.Add(dictRequestLoginUser[socketKey], socketKey);
                        dictRequestLoginUser.Remove(socketKey);

                        //将每个新产生的socket存起来，以客户端IP:端口作为key
                        dictSocket.Add(socketKey, socketConnection);

                        //为每个服务端通信socket创建一个单独的通信线程，负责调用通信socket的Receive方法，监听客户端发来的数据
                        //创建通信线程
                        Thread threadCommunicate = new Thread(ReceiveMsg);
                        threadCommunicate.IsBackground = true;
                        threadCommunicate.Start(socketConnection);

                        dictThread.Add(socketKey, threadCommunicate);

                        Console.WriteLine("用户 : {0} IP : {1} 已连接...");
                    }
                    else
                        return;
                }
                catch (SocketException se)
                {
                    Console.WriteLine("【错误】" + se.Message);
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("【错误】" + e.Message);
                    return;
                }
            }
        }
        #endregion

        #region ----------     监听数据     ----------
        /// <summary>
        ///     服务端监听客户发来的数据
        /// </summary>
        void ReceiveMsg(object socketClientPara)
        {
            Socket socketClient = socketClientPara as Socket;

            while(true)
            {
                // 定义一个接收消息用的缓冲区
                byte[] msgReceiver = new byte[1024 * 1024 * 2];
                
                // 接收消息的长度
                int length = -1;

                try
                {
                    length = socketClient.Receive(msgReceiver);
                }
                catch (SocketException se)
                {
                    string socketKey = socketClient.RemoteEndPoint.ToString();
                    Console.WriteLine("【错误】" + socketKey + " 接收消息异常 错误信息：" + se.Message);

                    // 将出错对象相关联的信息从队列中移除
                    dictOnlineUser.Remove(socketKey);
                    dictSocket.Remove(socketKey);
                    dictThread.Remove(socketKey);

                    // TODO: 更新客户端显示的在线好友

                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("【错误】" + e.Message);
                    return;
                }

                if (msgReceiver[0] == LOGIN)                            // 判断客户端信息发来的第一位，如果是0代表是登录请求
                {
                    // TODO: 查询数据库，判断登录状态
                }
                else if (msgReceiver[0] % 3 == SENDTOINDIVIDULAL)       // 如果是1，则是发给特定用户的信息或文件
                {
                    // TODO: 发送消息给特定用户
                    string sendMsg = Encoding.UTF8.GetString(msgReceiver, 0, length);
                    SendMsgToIndividual(sendMsg);
                }
                else if (msgReceiver[0] % 3 == SENDTOALL)               // 如果是2，则是发给所有用户的信息或文件
                {
                    // TODO：发送消息给所有用户
                    string sendMsg = Encoding.UTF8.GetString(msgReceiver, 0, length);
                    SendMsgToAll(sendMsg);
                }
                else                                                    //消息传输错误
                {
                    string socketKey = socketClient.RemoteEndPoint.ToString();
                    Console.WriteLine("【错误】" + socketKey + " 接收消息异常");

                    // 将出错对象相关联的信息从队列中移除
                    dictOnlineUser.Remove(socketKey);
                    dictSocket.Remove(socketKey);
                    dictThread.Remove(socketKey);

                    // TODO: 更新客户端显示的在线好友
                }
            }
        }
        #endregion

        #region ---------- 发送消息给特定用户 ----------
        /// <summary>
        ///     发送消息给特定用户
        /// </summary>
        /// <param name="Msg">要发送的消息</param>
        void SendMsgToIndividual(string Msg)
        {
            // 定义一个消息解析对象，用于查找发送个体
            string str = Msg.Substring(1, Msg.Length - 1);
            MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(str, typeof(MsgHandler));

            // 找到目标IP
            string targetIp;
            if (msgHandler.to != null)
                targetIp = dictOnlineUserO[msgHandler.to];
            else
            {
                Console.WriteLine("【错误】【非法消息】此消息不含接收端信息！");
                return;
            }

            //若目标IP不在在线队列中
            if (targetIp == null || targetIp == "")
            {
                Console.WriteLine("【错误】【非法消息】此消息接收端不在消息队列中！");
                return;
            }

            try
            {
                //将要传输的字符串转换成UTF-8对应的字节数组
                byte[] arrMsg = Encoding.UTF8.GetBytes(Msg);
                //向目标IP所对应的socket发送消息
                dictSocket[targetIp].Send(arrMsg);
            }
            catch (SocketException se)
            {
                Console.WriteLine("【错误】发送消息时出现错误：" + se.Message);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("【错误】发送消息时出现错误：" + e.Message);
                return;
            }

        }
        #endregion

        #region --------- 发送消息给所有用户 ----------
        /// <summary>
        ///     发送消息给所有用户
        /// </summary>
        /// <param name="Msg">要发送的消息</param>
        void SendMsgToAll(string Msg)
        {
            // 用UTF8解码成字节数组
            byte[] strMsg = Encoding.UTF8.GetBytes(Msg);

            // 解析字符串，在发送时不向发送者发送消息
            MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(Msg.Substring(1, Msg.Length - 1), typeof(MsgHandler));

            // 获取发送者的Ip信息
            string senderIp;
            if (msgHandler.from != null)
                senderIp = dictOnlineUserO[msgHandler.from];
            else    // 消息不包含发送者信息报错 
            {
                Console.WriteLine("【错误】【非法消息】消息 \"" + Msg.Substring(1, Msg.Length - 1) + "\" 不包含发送者信息！");
                return;
            }

            // 若不包含发送者的Socket报错
            if (senderIp == null || senderIp == "")
            {
                Console.WriteLine("【错误】【非法消息】消息 \"" + Msg.Substring(1, Msg.Length - 1) + "\" 发送者不在已存数据中！");
                return;
            }

            // 群发消息
            foreach (string s in dictSocket.Keys)
            {
                if (s != senderIp)
                {
                    try
                    {
                        dictSocket[s].Send(strMsg);
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("【错误】群发过程出错：" + se.Message);
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("【错误】群发过程出错：" + e.Message);
                        return;
                    }
                }
            }
            Console.WriteLine("群发成功");
        }
        #endregion

        #region -------- 用于解析数据的结构体 --------
        /// <summary>
        ///     用于JSON解析登录的结构体
        /// </summary>
        struct LoginHandler
        {
            public string userName;
            public string password;

            public LoginHandler(string n, string p)
            {
                userName = n; password = p;
            }
        }

        /// <summary>
        ///     用于JSON解析消息通信的结构体
        /// </summary>
        struct MsgHandler
        {
            public string from;
            public string to;
            public string message;

            public MsgHandler(int o, string f, string t, string m)
            {
                from = f; to = t; message = m;
            }
        };
        #endregion
    }

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
