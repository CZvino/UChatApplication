using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UChatClient
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
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

        private const string ipAddr = "127.0.0.1";  //连接ip
        private const int port = 3000;              //连接port

        private bool flagLogin;
        private UserData userData;

        // 客户端负责接收服务端发来的数据消息的线程
        private Thread threadClient = null;
        // 创建客户端socket，负责连接服务器
        private Socket socketClient = null;

        // 用于保存在线的好友列表
        private List<string> friendList;

        #endregion

        #region ----------     连接服务器     ----------
        /// <summary>
        ///     初始化，与服务器进行连接
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            flagLogin = false;

            //获得文本框中的IP地址对象
            IPAddress address = IPAddress.Parse(ipAddr);
            //创建包含IP和端口的网络节点对象
            IPEndPoint endpoint = new IPEndPoint(address, port);
            //创建客户端套接字，负责连接服务器
            socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                //客户端连接到服务器
                socketClient.Connect(endpoint);

                threadClient = new Thread(ReceiveMsg);
                threadClient.IsBackground = true;
                threadClient.Start();
            }
            catch (SocketException se)
            {
                MessageBox.Show("服务器连接失败：" + se.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("服务器连接失败：" + ex.Message);
            }
        }
        #endregion

        #region ----------    限制输入规则    ----------
        /// <summary>
        ///     判断输入非数字是不允许输入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void accountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9.-]+");
            e.Handled = re.IsMatch(e.Text);
        }

        /// <summary>
        ///     当用户粘贴文本时判断粘贴的文本是否为8位数字
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void accountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string str = accountTextBox.Text;
            for (int i = 0; i < str.Length; i++)
                if (str[i] < '0' || str[i] > '9')
                {
                    accountTextBox.Text = "";
                    break;
                }

            if (str.Length > 8)
                accountTextBox.Text = str.Substring(0, 8);

        }
        #endregion

        #region ----------        登录        ----------
        /// <summary>
        ///     登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Login_Click(object sender, RoutedEventArgs e)
        {

            LoginHandler loginHandler = new LoginHandler(accountTextBox.Text, passwordTextBox.Password);

            string msgPackage = JsonConvert.SerializeObject(loginHandler);

            byte[] arrMsg = Encoding.UTF8.GetBytes(msgPackage);
            byte[] sendArrMsg = new byte[arrMsg.Length + 1];

            // 设置标志位，代表登录
            sendArrMsg[0] = LOGIN;
            Buffer.BlockCopy(arrMsg, 0, sendArrMsg, 1, arrMsg.Length);

            try
            {
                socketClient.Send(sendArrMsg);
            }
            catch (SocketException se)
            {
                Console.WriteLine("【错误】发送消息异常：" + se.Message);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("【错误】发送消息异常：" + ex.Message);
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
                    if (arrMsg[0] == IS_LOGIN)
                    {
                        userData = (UserData)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(UserData));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { LoginSuccess(); }));

                    }
                    else if (arrMsg[0] == IS_NOT_LOGIN)
                    {
                        //Application.Current.Dispatcher.Invoke(new Action(delegate { AddText(); }));
                        
                        this.hint.Dispatcher.Invoke(new Action(() =>
                        {
                            this.hint.Content = "输入的账号或密码有错误!";
                        }));
                        this.accountTextBox.Dispatcher.Invoke(new Action(() =>
                        {
                            this.accountTextBox.Text = "";
                        }));
                        this.passwordTextBox.Dispatcher.Invoke(new Action(() =>
                        {
                            this.passwordTextBox.Password = "";
                        }));
                    }
                    else if (arrMsg[0] == MSG_TO_ALL || arrMsg[0] == MSG_TO_INDIVIDULAL)
                    {
                        // 普通消息
                        MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(MsgHandler));
                        Console.WriteLine("{0} : {1}", msgHandler.from, msgHandler.message);
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

        #region ---------- 登陆成功更新用户信息 ----------
        /// <summary>
        ///     代理更新登录画布显示的元素（跨线程更改）
        /// </summary>
        private void LoginSuccess()
        {
            flagLogin = true;

            userInfoCanvas.Children.Remove(accountLabel);
            userInfoCanvas.UnregisterName("accountLabel");
            userInfoCanvas.Children.Remove(accountTextBox);
            userInfoCanvas.UnregisterName("accountTextBox");
            userInfoCanvas.Children.Remove(passwordLabel);
            userInfoCanvas.UnregisterName("passwordLabel");
            userInfoCanvas.Children.Remove(passwordTextBox);
            userInfoCanvas.UnregisterName("passwordTextBox");
            userInfoCanvas.Children.Remove(Login);
            userInfoCanvas.UnregisterName("Login");
            userInfoCanvas.Children.Remove(Register);
            userInfoCanvas.UnregisterName("Register");

            Label userIdTag = new Label();
            {
                userIdTag.Width = 60;
                userIdTag.Height = 25;
                userIdTag.Content = "账   号：";
                userIdTag.VerticalContentAlignment = VerticalAlignment.Center;
                userIdTag.HorizontalContentAlignment = HorizontalAlignment.Center;
                userIdTag.Margin = new Thickness(25, 10, 0, 0);
            }

            Label userId = new Label();
            {
                userId.Width = 100;
                userId.Height = 25;
                userId.Content = userData.userId;
                userId.VerticalContentAlignment = VerticalAlignment.Center;
                userId.HorizontalContentAlignment = HorizontalAlignment.Left;
                userId.Margin = new Thickness(90, 10, 0, 0);
            }

            Label userNameTag = new Label();
            {
                userNameTag.Width = 60;
                userNameTag.Height = 25;
                userNameTag.Content = "用户名：";
                userNameTag.VerticalContentAlignment = VerticalAlignment.Center;
                userNameTag.HorizontalContentAlignment = HorizontalAlignment.Center;
                userNameTag.Margin = new Thickness(25, 40, 0, 0);
            }

            Label userName = new Label();
            {
                userName.Width = 100;
                userName.Height = 25;
                userName.Content = userData.userName;
                userName.VerticalContentAlignment = VerticalAlignment.Center;
                userName.HorizontalContentAlignment = HorizontalAlignment.Left;
                userName.Margin = new Thickness(90, 40, 0, 0);
            }

            Label userGenderTag = new Label();
            {
                userGenderTag.Width = 60;
                userGenderTag.Height = 25;
                userGenderTag.Content = "性   别：";
                userGenderTag.VerticalContentAlignment = VerticalAlignment.Center;
                userGenderTag.HorizontalContentAlignment = HorizontalAlignment.Center;
                userGenderTag.Margin = new Thickness(25, 70, 0, 0);
            }

            Label userGender = new Label();
            {
                userGender.Width = 100;
                userGender.Height = 25;
                userGender.Content = userData.userGender;
                userGender.VerticalContentAlignment = VerticalAlignment.Center;
                userGender.HorizontalContentAlignment = HorizontalAlignment.Left;
                userGender.Margin = new Thickness(90, 70, 0, 0);
            }

            Label userAgeTag = new Label();
            {
                userAgeTag.Width = 60;
                userAgeTag.Height = 25;
                userAgeTag.Content = "年   龄：";
                userAgeTag.VerticalContentAlignment = VerticalAlignment.Center;
                userAgeTag.HorizontalContentAlignment = HorizontalAlignment.Center;
                userAgeTag.Margin = new Thickness(25, 100, 0, 0);
            }

            Label userAge = new Label();
            {
                userAge.Width = 100;
                userAge.Height = 25;
                userAge.Content = userData.userAge;
                userAge.VerticalContentAlignment = VerticalAlignment.Center;
                userAge.HorizontalContentAlignment = HorizontalAlignment.Left;
                userAge.Margin = new Thickness(90, 100, 0, 0);
            }

            Button editInfo = new Button();
            {
                editInfo.Width = 50;
                editInfo.Height = 20;
                editInfo.Content = "编辑";
                editInfo.VerticalContentAlignment = VerticalAlignment.Center;
                editInfo.HorizontalContentAlignment = HorizontalAlignment.Center;
                editInfo.Margin = new Thickness(150, 130, 0, 0);
            }

            userInfoCanvas.Children.Add(userIdTag);
            userInfoCanvas.Children.Add(userId);
            userInfoCanvas.Children.Add(userNameTag);
            userInfoCanvas.Children.Add(userName);
            userInfoCanvas.Children.Add(userGenderTag);
            userInfoCanvas.Children.Add(userGender);
            userInfoCanvas.Children.Add(userAgeTag);
            userInfoCanvas.Children.Add(userAge);
            userInfoCanvas.Children.Add(editInfo);
        }
        #endregion
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
        public UserData friendInfo;

        public FriendlistHandler(int t, UserData f)
        {
            type = t; friendInfo = f;
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
