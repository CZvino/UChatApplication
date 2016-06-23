using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace UChatClientConsole
{
    class UChatClientConsole
    {
        #region ----------     定义变量     ----------

        #region ----------    定义状态字    ----------
        // 登录/注册 相关
        private const byte LOGIN_OR_REGISTER = 0;
        private const byte IS_LOGIN_OR_REGISTER = 1;

        // 用户发送给单个用户 相关
        private const byte INDIVIDUAL_LOWER_BOUND = 10;
        private const byte INDIVIDUAL_UPPER_BOUND = 19;
        private const byte MSG_TO_INDIVIDULAL = 10;
        private const byte FILE_TO_INDIVIDUAL = 11;
        private const byte PICTURE_TO_INDIVIDUAL = 12;

        // 用户广播信息 相关
        private const byte ALL_LOWER_BOUND = 20;
        private const byte ALL_UPPER_BOUND = 29;
        private const byte MSG_TO_ALL = 20;
        private const byte FILE_TO_ALL = 21;
        private const byte PICTURE_TO_ALL = 22;

        // 服务器向用户提交更新好友状态相关
        private const byte UPDATE_FRIENDLIST = 30;

        // 登录注册状态字
        private const int LOGIN = 0;
        private const int REGISTER = 1;

        // 在线好友列表状态字
        private const int ADD_ONLINE_FRIEND = 0;
        private const int REMOVE_ONLINE_FRIEND = 1;
        #endregion

        private const string ipAddr = "127.0.0.1";  //连接ip
        private const int port = 3000;              //连接port

        // 客户端负责接收服务端发来的数据消息的线程
        private Thread threadClient = null;
        // 创建客户端socket，负责连接服务器
        private Socket socketClient = null;

        // 用于保存在线的好友列表
        private List<string> friendList;

        private string userName;
        private string toName;

        #endregion

        public UChatClientConsole()
        {
        }

        public UChatClientConsole(string name, string to)
        {
            userName = name;
            toName = to;
        }

        #region ----------    开启客户机    ----------
        /// <summary>
        ///     开启客户端
        /// </summary>
        public void StartClient()
        {
            //创建一个绑定IP和port的网络节点对象
            IPAddress ipAddress = IPAddress.Parse(ipAddr);
            IPEndPoint endPoint = new IPEndPoint(ipAddress, port);

            socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                //连接服务器
                socketClient.Connect(endPoint);

                threadClient = new Thread(ReceiveMsg);
                threadClient.IsBackground = true;
                threadClient.Start();
            }
            catch (SocketException se)
            {
                Console.WriteLine("【错误】连接服务器异常：" + se.Message);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("【错误】连接服务器异常：" + e.Message);
                return;
            }
        }
        #endregion

        #region ----------     接收消息     ----------
        /// <summary>
        ///     接收消息
        /// </summary>
        private void ReceiveMsg()
        {
            while (true)
            {
                // 定义一个缓冲区保存接收的消息
                byte[] arrMsg = new byte[1024 * 1024 * 2];

                // 保存数据长度
                int length = -1;

                try
                {
                    // 获取信息
                    length = socketClient.Receive(arrMsg);

                    // 解析字符串
                    string msgReceive = Encoding.UTF8.GetString(arrMsg);                  

                    // 消息
                    if (arrMsg[0] / 4 == 0)
                    {
                        // 登录信息
                        if (arrMsg[0] == 3)
                        {
                            // TODO: 显示好友登录的信息
                        }
                        // 普通消息
                        else
                        {
                            MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(MsgHandler));
                            Console.WriteLine("{0} : {1}", msgHandler.from, msgHandler.message);
                        }
                    }
                    // 文件
                    else if (arrMsg[0] / 4 == 1)
                    {
                        // TODO: 保存文件
                    }
                }
                catch (SocketException se)
                {
                    Console.WriteLine("【错误】接收消息异常：" + se.Message);
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("【错误】接收消息异常：" + e.Message);
                    return;
                }
            }
        }
        #endregion

        #region ----------     发送消息     ----------
        public void SendMsg(string msg)
        {
            MsgHandler msgHandler = new MsgHandler();
            msgHandler.from = userName;
            msgHandler.to = toName;
            msgHandler.message = msg;
            string msgPackage = JsonConvert.SerializeObject(msgHandler);

            byte[] arrMsg = Encoding.UTF8.GetBytes(msgPackage);
            byte[] sendArrMsg = new byte[arrMsg.Length + 1];
            
            // 设置标志位，代表发送消息给个人
            sendArrMsg[0] = MSG_TO_INDIVIDULAL;
            Buffer.BlockCopy(arrMsg, 0, sendArrMsg, 1, arrMsg.Length);

            try
            {
                socketClient.Send(sendArrMsg);

                Console.WriteLine("我：{0}", msg);
            }
            catch (SocketException se)
            {
                Console.WriteLine("【错误】发送消息异常：" + se.Message);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("【错误】发送消息异常：" + e.Message);
                return;
            }

        }

        public void Login()
        {
            LoginHandler loginHandler = new LoginHandler(LOGIN, userName, "");

            string msgPackage = JsonConvert.SerializeObject(loginHandler);

            byte[] arrMsg = Encoding.UTF8.GetBytes(msgPackage);
            byte[] sendArrMsg = new byte[arrMsg.Length + 1];

            // 设置标志位，代表登录
            sendArrMsg[0] = LOGIN_OR_REGISTER;
            Buffer.BlockCopy(arrMsg, 0, sendArrMsg, 1, arrMsg.Length);

            try
            {
                socketClient.Send(sendArrMsg);

                Console.WriteLine("{0} 登录", userName);
            }
            catch (SocketException se)
            {
                Console.WriteLine("【错误】发送消息异常：" + se.Message);
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine("【错误】发送消息异常：" + e.Message);
                return;
            }

        }
        #endregion

        #region -------- 用于解析数据的结构体 --------
        /// <summary>
        ///     用于JSON解析登录的结构体
        /// </summary>
        struct LoginHandler
        {
            public int type;
            public string userName;
            public string password;

            public LoginHandler(int t, string n, string p)
            {
                type = t; userName = n; password = p;
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

        /// <summary>
        ///     用于JSON解析更新在线好友列表的信息
        /// </summary>
        struct FriendlistHandler
        {
            public int type;
            public string friendName;

            public FriendlistHandler(int t, string f)
            {
                type = t; friendName = f;
            }
        }
        #endregion
    }
}
