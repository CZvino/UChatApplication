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
        #region ----------      定义变量      ----------

        #region ----------     定义状态字     ----------
        // 登录/注册 相关
        private const byte LOGIN = 0;
        private const byte REGISTER = 1;
        private const byte IS_LOGIN = 2;
        private const byte IS_NOT_LOGIN = 3;
        private const byte IS_REGISTER = 4;
        private const byte IS_NOT_REGISTER = 5;

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
        private const byte INIT_FRIENDLIST = 31;

        // 在线好友列表状态字
        private const int ADD_ONLINE_FRIEND = 0;
        private const int REMOVE_ONLINE_FRIEND = 1;
        #endregion

        private DatabaseHandler databaseHandler;    //数据库连接对象

        private const string ipAddr = "127.0.0.1";  //监听ip
        private const int port = 3000;              //监听port

        private Thread threadWatch = null;          //负责监听客户端请求的线程
        private Socket socketWatch = null;          //负责监听服务端的嵌套字

        //保存了服务器端所有负责调用通信套接字的Receive方法的线程
        private Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();
        //保存了所有客户端通信的套接字和客户端的用户名
        private Dictionary<string, Socket> dictSocket = new Dictionary<string, Socket>();
        //保存了服务器端所有和客户端通信的套接字
        private Dictionary<string, string> dictOnlineUser = new Dictionary<string, string>();
        private Dictionary<string, string> dictOnlineUserO = new Dictionary<string, string>();

        #endregion

        public UChatServer()
        {
            databaseHandler = new DatabaseHandler();
        }

        #region ----------     开启服务器     ----------
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

        #region ----------   监听客户端请求   ----------
        /// <summary>
        ///     监听客户端请求
        /// </summary>
        private void WatchConnection()
        {
            //持续监听请求
            while (true)
            {
                Socket socketConnection = null;
                try
                {
                    //监听请求，如果有新请求则返回一个socket
                    socketConnection = socketWatch.Accept();
                    string socketKey = socketConnection.RemoteEndPoint.ToString();

                    //将每个新产生的socket存起来，以客户端IP:端口作为key
                    dictSocket.Add(socketKey, socketConnection);

                    //为每个服务端通信socket创建一个单独的通信线程，负责调用通信socket的Receive方法，监听客户端发来的数据
                    //创建通信线程
                    Thread threadCommunicate = new Thread(ReceiveMsg);
                    threadCommunicate.IsBackground = true;
                    threadCommunicate.Start(socketConnection);

                    dictThread.Add(socketKey, threadCommunicate);

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

        #region ----------      监听数据      ----------
        /// <summary>
        ///     服务端监听客户发来的数据
        /// </summary>
        private void ReceiveMsg(object socketClientPara)
        {
            Socket socketClient = socketClientPara as Socket;

            while (true)
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

                    // TODO: 更新客户端显示的在线好友

                    // 将出错对象相关联的信息从队列中移除
                    dictOnlineUser.Remove(socketKey);
                    dictSocket.Remove(socketKey);
                    dictThread.Remove(socketKey);

                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("【错误】" + e.Message);
                    return;
                }

                // 判断客户端信息发来的第一位，如果是LOGIN代表是登录请求
                if (msgReceiver[0] == LOGIN)                            
                {
                    // 查询数据库，判断登录状态
                    string loginMsg = Encoding.UTF8.GetString(msgReceiver, 1, length-1);
                    LoginHandler loginHandler = (LoginHandler)JsonConvert.DeserializeObject(loginMsg, typeof(LoginHandler));

                    // 登录 将登录状态返回到flagLogin
                    UserData userData;
                    bool flagLogin = databaseHandler.Login(loginHandler.userId, loginHandler.userPassword, out userData);

                    if (flagLogin == true)
                    {
                        Console.WriteLine("用户 : {0} IP : {1} 已连接...", loginHandler.userId, socketClient.RemoteEndPoint.ToString());

                        dictOnlineUser.Add(socketClient.RemoteEndPoint.ToString(), loginHandler.userId);
                        dictOnlineUserO.Add(loginHandler.userId, socketClient.RemoteEndPoint.ToString());

                        // 确认登录信息
                        ConfirmLogin(IS_LOGIN, userData);

                        // TODO : 更新在线好友列表
                    }
                    else
                    {
                        Console.WriteLine("用户 : {0} IP : {1} 试图连接，但密码错误...", loginHandler.userId, socketClient.RemoteEndPoint.ToString());

                        // 确认登录失败
                        ConfirmLogin(IS_NOT_LOGIN, userData);
                    }

                    #region Use For Test
                    /* 功能测试

                    string loginMsg = Encoding.UTF8.GetString(msgReceiver, 1, length - 1);
                    LoginHandler loginHandler = (LoginHandler)JsonConvert.DeserializeObject(loginMsg, typeof(LoginHandler));

                    Console.WriteLine("用户 : {0} IP : {1} 已连接...", loginHandler.userName, socketClient.RemoteEndPoint.ToString());

                    dictOnlineUser.Add(socketClient.RemoteEndPoint.ToString(), loginHandler.userName);
                    dictOnlineUserO.Add(loginHandler.userName, socketClient.RemoteEndPoint.ToString());

                       功能测试end */
                    #endregion
                }
                //
                else if (msgReceiver[0] == REGISTER)
                {
                    // TODO: 处理注册请求

                }
                //
                else if (msgReceiver[0] == INIT_FRIENDLIST)
                {
                    // TODO: 用户登陆成功，需要初始化已在线的好友列表

                }
                // 如果在INDIVIDUAL_LOWER_BOUND和INDIVIDUAL_UPPER_BOUND之间，则是发给特定用户的信息或文件
                else if (msgReceiver[0] >= INDIVIDUAL_LOWER_BOUND && msgReceiver[0] <= INDIVIDUAL_UPPER_BOUND )       
                {
                    // 发送消息给特定用户
                    string sendMsg = Encoding.UTF8.GetString(msgReceiver, 0, length);
                    SendMsgToIndividual(sendMsg);
                }
                // 如果在ALL_LOWER_BOUND和ALL_UPPER_BOUND之间，则是发给所有用户的信息或文件
                else if (msgReceiver[0] >= ALL_LOWER_BOUND && msgReceiver[0] <= ALL_UPPER_BOUND)               
                {
                    // 发送消息给所有用户
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
        private void SendMsgToIndividual(string Msg)
        {
            // 定义一个消息解析对象，用于查找发送个体
            string str = Msg.Substring(1, Msg.Length - 1);
            MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(str, typeof(MsgHandler));

            // 找到目标IP
            string targetIp;
            if (msgHandler.to != null && dictOnlineUserO.ContainsKey(msgHandler.to))
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

        #region ---------- 发送消息给所有用户 ----------
        /// <summary>
        ///     发送消息给所有用户
        /// </summary>
        /// <param name="Msg">要发送的消息</param>
        private void SendMsgToAll(string Msg)
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

        #region ----------   确认登录或注册   ----------
        /// <summary>
        ///     确认登录信息
        /// </summary>
        /// <param name="flag">IS_LOGIN或者IS_NOT_LOGIN</param>
        /// <param name="userData">用户信息</param>
        private void ConfirmLogin(byte flag, UserData userData)
        {
            string sendMsg;

            if (flag == IS_LOGIN || flag == IS_REGISTER)
                sendMsg = JsonConvert.SerializeObject(userData);
            else
                sendMsg = "";

            byte[] arrMsg = Encoding.UTF8.GetBytes(sendMsg);
            byte[] sendArrMsg = new byte[arrMsg.Length + 1];

            // 设置标志位，代表登录/注册
            sendArrMsg[0] = flag;
            Buffer.BlockCopy(arrMsg, 0, sendArrMsg, 1, arrMsg.Length);

            // 获取发送者的Ip信息
            string senderIp = dictOnlineUserO[userData.userId];

            // 若不包含发送者的Socket报错
            if (senderIp == null || senderIp == "")
            {
                Console.WriteLine("【错误】【非法消息】消息 \"" + sendMsg.Substring(1, sendMsg.Length - 1) + "\" 发送者不在已存数据中！");
                return;
            }
            else
                dictSocket[senderIp].Send(sendArrMsg);

        }
        #endregion

        private void InitFriendlist(string userId, List<UserData> friendlist)
        {
            // TODO: 将friendlist中的所
        }

        private void UpdateFriendlist(byte flag, UserData userData)
        {
            // TODO: 更新好友列表
        }
    }

    #region -------- 用于解析数据的结构体 --------
    /// <summary>
    ///     用于JSON解析登录的结构体
    /// </summary>
    public struct LoginHandler
    {
        public string userId;
        public string userPassword;

        public LoginHandler(string n, string p)
        {
            userId = n; userPassword = p;
        }
    }

    /// <summary>
    ///     用于JSON解析注册的结构体
    /// </summary>
    public struct RegisterHandler
    {
        public string userId;
        public string userName;
        public string userPassword;
        public string userGender;
        public int userAge;

        public RegisterHandler(string id, string name, string password, string gender, int age)
        {
            userId = id; userName = name; userPassword = password; userGender = gender; userAge = age;
        }

        public RegisterHandler(string id, string name, string password)
        {
            userId = id; userName = name; userPassword = password; userGender = "男"; userAge = 0;
        }

        public RegisterHandler(string id, string name, string password, string gender)
        {
            userId = id; userName = name; userPassword = password; userGender = gender; userAge = 0;
        }

        public RegisterHandler(string id, string name, string password, int age)
        {
            userId = id; userName = name; userPassword = password; userGender = "男"; userAge = age;
        }
    }

    /// <summary>
    ///     用于JSON解析消息通信的结构体
    /// </summary>
    public struct MsgHandler
    {
        public string from;
        public string to;
        public string message;

        public MsgHandler(string f, string t, string m)
        {
            from = f; to = t; message = m;
        }
    };

    /// <summary>
    ///     用于JSON解析更新在线好友列表的信息
    /// </summary>
    public struct FriendlistHandler
    {
        public int type;
        public string friendName;

        public FriendlistHandler(int t, string f)
        {
            type = t; friendName = f;
        }
    }

    /// <summary>
    ///     用于保存从数据库中查找到的用户信息
    /// </summary>
    public struct UserData
    {
        public string userId;
        public string userName;
        public string userGender;
        public int userAge;

        public UserData(string id, string name, string gender, int age)
        {
            userId = id; userName = name; userGender = gender; userAge = age;
        }
    }
    #endregion
}
