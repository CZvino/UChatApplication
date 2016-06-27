using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
        private const byte UPDATE_USER_INFO = 6;
        private const byte IS_UPDATE = 7;
        private const byte IS_NOT_UPDATE = 8;
        private const byte DISCONNECT = 9;

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

        // 增删好友状态字
        private const byte ADD_FRIEND = 40;
        private const byte SUB_FRIEND = 41;

        // 在线好友列表状态字
        private const int ADD_ONLINE_FRIEND = 0;
        private const int REMOVE_ONLINE_FRIEND = 1;

        #endregion

        private const string ipAddr = "127.0.0.1";  //连接ip
        private const int port = 3000;              //连接port

        private bool addFriendFlag, subFriendFlag;
        private UserData userData;
        private string password;

        // 客户端负责接收服务端发来的数据消息的线程
        private Thread threadClient = null;
        // 创建客户端socket，负责连接服务器
        private Socket socketClient = null;

        // 用于保存在线的好友列表
        private ObservableCollection<onlineFriendType> onlineFriendlist;
        private Dictionary<string, List<Paragraph>> dictParagraph = new Dictionary<string, List<Paragraph>>();
        private Dictionary<string, Thread> dictAnimation = new Dictionary<string, Thread>();

        #endregion

        #region ----------     连接服务器     ----------
        /// <summary>
        ///     初始化，与服务器进行连接
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            onlineFriendlist = new ObservableCollection<onlineFriendType>();
            onlineFriendlistListBox.ItemsSource = onlineFriendlist;

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

            List<Paragraph> paragraphList = new List<Paragraph>();
            dictParagraph.Add("AllUsers", paragraphList);
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
            password = passwordTextBox.Password;
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
                    else if (arrMsg[0] == IS_REGISTER)
                    {
                        UserData userdata = (UserData)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(UserData));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { RegisterSuccess(userdata); }));
                    }
                    else if (arrMsg[0] == IS_NOT_REGISTER)
                    {
                        MessageBox.Show("注册失败!");
                    }
                    else if (arrMsg[0] == IS_UPDATE)
                    {
                        userData = (UserData)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(UserData));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { UpdateInfoSuccess(); }));
                    }
                    else if (arrMsg[0] == IS_NOT_UPDATE)
                    {
                        MessageBox.Show("更新信息失败！");
                    }
                    else if (arrMsg[0] == UPDATE_FRIENDLIST)
                    {
                        FriendlistHandler friendlistHandler = (FriendlistHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(FriendlistHandler));
                        if (friendlistHandler.type == ADD_ONLINE_FRIEND)
                        {
                            bool flag = true;
                            foreach (onlineFriendType oFT in onlineFriendlist)
                            {
                                if (oFT.userData.userId != null && oFT.userData.userId.Equals(friendlistHandler.friendInfo.userId))
                                {
                                    flag = false;
                                }
                            }
                            if (flag)
                            {
                                onlineFriendType newFriend = new onlineFriendType(friendlistHandler.friendInfo);
                                try
                                {
                                    Application.Current.Dispatcher.Invoke(new Action(delegate
                                    {
                                        // 新增一条在线好友信息
                                        onlineFriendlist.Add(newFriend);
                                        // 为新上线的好友分配一个聊天框
                                        List<Paragraph> paragraph = new List<Paragraph>();
                                        dictParagraph.Add(newFriend.userData.userId, paragraph);
                                    }));
                                }
                                catch (Exception e)
                                {
                                    MessageBox.Show(e.Message);
                                }
                            }
                        }
                        else if (friendlistHandler.type == REMOVE_ONLINE_FRIEND)
                        {

                            foreach (onlineFriendType oFT in onlineFriendlist)
                            {
                                if (oFT.userData.userId != null && oFT.userData.userId.Equals(friendlistHandler.friendInfo.userId))
                                {
                                    try
                                    {
                                        Application.Current.Dispatcher.Invoke(new Action(delegate
                                        {
                                            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;
                                            if (sendTo.nameAndId.Equals("所有人") == false && sendTo.userData.userId.Equals(oFT.userData.userId))
                                            {
                                                onlineFriendlistListBox.SelectedIndex = 0;

                                                chatRichTextBox.Document.Blocks.Clear();
                                                foreach (Paragraph p in dictParagraph["AllUsers"])
                                                    chatRichTextBox.Document.Blocks.Add(p);
                                            }

                                            // 将下线好友的聊天框移除
                                            dictParagraph.Remove(oFT.userData.userId);
                                            // 将下线好友从在线好友列表中移除
                                            onlineFriendlist.Remove(oFT);
                                        }));
                                        break;
                                    }
                                    catch (Exception e)
                                    {
                                        MessageBox.Show(e.Message);
                                    }
                                }
                            }
                        }
                    }
                    else if (arrMsg[0] == MSG_TO_ALL)
                    {
                        MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(MsgHandler));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { ReceiveMsgForAll(msgHandler); }));
                    }
                    else if (arrMsg[0] == MSG_TO_INDIVIDULAL)
                    {
                        MsgHandler msgHandler = (MsgHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(MsgHandler));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { ReceiveMsgForIndividual(msgHandler); }));
                    }
                    else if (arrMsg[0] == FILE_TO_ALL)
                    {
                        FileHandler msgHandler = (FileHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(FileHandler));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { ReceiveFileForAll(msgHandler); }));
                    }
                    else if (arrMsg[0] == FILE_TO_INDIVIDUAL)
                    {
                        FileHandler msgHandler = (FileHandler)JsonConvert.DeserializeObject(msgReceive.Substring(1, msgReceive.Length - 1), typeof(FileHandler));

                        Application.Current.Dispatcher.Invoke(new Action(delegate { ReceiveFileForIndividual(msgHandler); }));
                    }
                    else
                    {
                        MessageBox.Show(Encoding.UTF8.GetString(arrMsg));
                    }
                }
                catch (SocketException se)
                {
                    MessageBox.Show("【错误】接收消息异常：" + se.Message);
                    return;
                }
                catch (Exception e)
                {
                    MessageBox.Show("【错误】接收消息异常：" + e.Message);
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

            mainWindow.Title = "U信 - " + userData.userName + "(" + userData.userId + ")";

            InitFriendList();

            addFriendFlag = subFriendFlag = false;

            userLoginCanvas.Visibility = Visibility.Hidden;
            userInfoCanvas.Visibility = Visibility.Visible;

            userId.Content = userData.userId;
            userName.Content = userData.userName;
            userGender.Content = userData.userGender;
            userAge.Content = userData.userAge;

            friendListName.Foreground = new SolidColorBrush(Colors.Black);
            inputTextBox.IsEnabled = true;
            fileTextBox.IsEnabled = true;
            chooseFile.IsEnabled = true;
            sendFile.IsEnabled = true;
            sendMsg.IsEnabled = true;
            addFriend.IsEnabled = true;
            subFriend.IsEnabled = true;
            chatRichTextBox.IsEnabled = true;
            onlineFriendlistListBox.IsEnabled = true;
            onlineFriendlistListBox.SelectedIndex = 0;
            chatFriendName.Content = "所有人";

            chatRichTextBox.Document.Blocks.Clear();
            foreach (Paragraph p in dictParagraph["AllUsers"])
                chatRichTextBox.Document.Blocks.Add(p);

            onlineFriendType All = new onlineFriendType("所有人");
            onlineFriendlist.Add(All);

        }
        #endregion

        #region ------  更新信息成功，更新本地信息  ------
        /// <summary>
        ///     更新信息成功，更新本地信息
        /// </summary>
        private void UpdateInfoSuccess()
        {
            mainWindow.Title = "U信 - " + userData.userName + "(" + userData.userId + ")";

            userId.Content = userData.userId;
            userName.Content = userData.userName;
            userGender.Content = userData.userGender;
            userAge.Content = userData.userAge;

            userEditInfoCanvas.Visibility = Visibility.Hidden;
            userInfoCanvas.Visibility = Visibility.Visible;
        }
        #endregion

        #region ----------    初始化好友列表    ----------
        /// <summary>
        ///     初始化好友列表
        /// </summary>
        private void InitFriendList()
        {
            // 向服务器发送断开连接申请
            byte[] sendArrMsg = new byte[1];
            sendArrMsg[0] = INIT_FRIENDLIST;

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

        #region ----------     重载关闭方法     ----------
        /// <summary>
        ///     重载关闭方法，增加断开连接的步骤
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 向服务器发送断开连接申请
            byte[] sendArrMsg = new byte[1];
            sendArrMsg[0] = DISCONNECT;

            try
            {
                socketClient.Send(sendArrMsg);
            }
            catch (SocketException se)
            {
                MessageBox.Show("【错误】发送消息异常：" + se.Message);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show("【错误】发送消息异常：" + ex.Message);
                return;
            }

            socketClient.Close();
            threadClient.Abort();

            // 关闭
            base.OnClosing(e);
        }
        #endregion

        #region ----------   修改用户个人信息   ----------
        /// <summary>
        ///     修改用户个人信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editInfo_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.Title = "U信 - 修改信息";

            userIdEdit.Content = userData.userId;
            userNameEdit.Text = userData.userName;
            userPasswordEdit.Text = password;
            if (userData.userGender.Trim().Equals("男"))
                userGenderEdit.SelectedItem = male;
            else
                userGenderEdit.SelectedItem = female;
            userAgeEdit.Text = Convert.ToString(userData.userAge);
            userInfoCanvas.Visibility = Visibility.Hidden;
            userEditInfoCanvas.Visibility = Visibility.Visible;
        }
        #endregion

        #region ----------       确认修改       ----------
        /// <summary>
        ///     确认修改
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editInfoConfirm_Click(object sender, RoutedEventArgs e)
        {
            // 修改信息
            RegisterHandler updateHandler = new RegisterHandler((string)userIdEdit.Content, userNameEdit.Text, userPasswordEdit.Text, userGenderEdit.Text, Convert.ToInt32(userAgeEdit.Text));
            string updateMsg = JsonConvert.SerializeObject(updateHandler);

            byte[] arrMsg = Encoding.UTF8.GetBytes(updateMsg);
            byte[] sendArrMsg = new byte[arrMsg.Length + 1];

            // 设置标志位，代表更新用户信息
            sendArrMsg[0] = UPDATE_USER_INFO;
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

        #region ----------       取消修改       ----------
        /// <summary>
        ///     撤销修改
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editInfoCancel_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.Title = "U信 - " + userData.userName + "(" + userData.userId + ")";

            userInfoCanvas.Visibility = Visibility.Visible;
            userEditInfoCanvas.Visibility = Visibility.Hidden;
        }
        #endregion

        #region ----------     信息格式检查     ----------
        /// <summary>
        ///     密码不能多于15位、年龄为大于0小于100的整数，用户名的长度不能超过15位
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void userInfoEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 修改信息
            // 密码
            if (userPasswordEdit.Text.Length > 15)
                userPasswordEdit.Text = userPasswordEdit.Text.Substring(0, 15);
            userPasswordEdit.Select(userPasswordEdit.Text.Length, 0);


            // 用户名
            if (userNameEdit.Text.Length > 15)
                userNameEdit.Text = userNameEdit.Text.Substring(0, 15);
            userNameEdit.Select(userNameEdit.Text.Length, 0);

            // 年龄
            for (int i = 0; i < userAgeEdit.Text.Length; i++)
                if (userAgeEdit.Text[i] < '0' || userAgeEdit.Text[i] > '9')
                    userAgeEdit.Text = "";

            if (userAgeEdit.Text.Length > 2)
                userAgeEdit.Text = userAgeEdit.Text.Substring(0, 2);

            userAgeEdit.Select(userAgeEdit.Text.Length, 0);

            // 注册
            // 密码
            if (userPasswordRegister.Text.Length > 15)
                userPasswordRegister.Text = userPasswordRegister.Text.Substring(0, 15);
            userPasswordRegister.Select(userPasswordRegister.Text.Length, 0);

            // 用户名
            if (userNameRegister.Text.Length > 15)
                userNameRegister.Text = userNameRegister.Text.Substring(0, 15);
            userNameRegister.Select(userNameRegister.Text.Length, 0);

            // 年龄
            for (int i = 0; i < userAgeRegister.Text.Length; i++)
                if (userAgeRegister.Text[i] < '0' || userAgeRegister.Text[i] > '9')
                    userAgeRegister.Text = "";

            if (userAgeRegister.Text.Length > 2)
                userAgeRegister.Text = userAgeRegister.Text.Substring(0, 2);

            userAgeRegister.Select(userAgeRegister.Text.Length, 0);

        }
        #endregion

        #region ----------  注册及相关按钮响应  ----------
        /// <summary>
        ///     注册按钮响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.Title = "U信 - 注册";

            userRegisterCanvas.Visibility = Visibility.Visible;
            userLoginCanvas.Visibility = Visibility.Hidden;
        }

        /// <summary>
        ///     确认注册
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void registerConfirm_Click(object sender, RoutedEventArgs e)
        {
            // 注册信息
            if (userNameRegister.Text.Equals(""))
                MessageBox.Show("用户名不得为空！");
            else if (userPasswordRegister.Text.Equals(""))
                MessageBox.Show("密码不得为空！");
            else
            {
                string gender = userGenderRegister.Text.Equals("") ? "男" : userGenderRegister.Text;
                int age = userAgeRegister.Text.Equals("") ? 0 : Convert.ToInt32(userAgeRegister.Text);

                RegisterHandler registerHandler = new RegisterHandler("", userNameRegister.Text, userPasswordRegister.Text, gender, age);
                string updateMsg = JsonConvert.SerializeObject(registerHandler);

                byte[] arrMsg = Encoding.UTF8.GetBytes(updateMsg);
                byte[] sendArrMsg = new byte[arrMsg.Length + 1];

                // 设置标志位，代表更新用户信息
                sendArrMsg[0] = REGISTER;
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

        }

        /// <summary>
        ///     取消注册
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void registerCancel_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.Title = "U信 - 登录";

            userNameRegister.Text = "";
            userPasswordRegister.Text = "";
            userGenderRegister.Text = "";
            userAgeRegister.Text = "";

            accountTextBox.Text = "";
            passwordTextBox.Password = "";
            userRegisterCanvas.Visibility = Visibility.Hidden;
            userLoginCanvas.Visibility = Visibility.Visible;
        }
        #endregion

        #region ----------       注册成功       ----------
        private void RegisterSuccess(UserData userdata)
        {
            MessageBox.Show("注册成功！\r\n您的账号为：" + userdata.userId + "\r\n您的密码为：" + userPasswordRegister.Text);
            userNameRegister.Text = "";
            userPasswordRegister.Text = "";
            userGenderRegister.Text = "";
            userAgeRegister.Text = "";

            accountTextBox.Text = "";
            passwordTextBox.Password = "";

            userRegisterCanvas.Visibility = Visibility.Hidden;
            userLoginCanvas.Visibility = Visibility.Visible;
        }
        #endregion

        #region ----------       发送消息       ----------
        /// <summary>
        ///     发送消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sendMsg_Click(object sender, RoutedEventArgs e)
        {
            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;

            byte[] arrMsg, sendArrMsg;

            Paragraph newParagraph = new Paragraph();

            Run run = new Run()
            {
                Text = "我  " + System.DateTime.Now.ToString() + "\r\n",
                Foreground = new SolidColorBrush(Colors.Blue)
            };
            newParagraph.Inlines.Add(run);

            InlineUIContainer inlineUIContainer = new InlineUIContainer()
            {

                Child = new TextBlock()
                {
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextWrapping = TextWrapping.Wrap,
                    Text = "  " + inputTextBox.Text + "\r\n"
                }
            };

            newParagraph.Inlines.Add(inlineUIContainer);

            if (sendTo.nameAndId.Equals("所有人"))
            {
                MsgHandler msgHandler = new MsgHandler(userData.userId, userData.userName, "", inputTextBox.Text);

                string sendMsg = JsonConvert.SerializeObject(msgHandler);
                arrMsg = Encoding.UTF8.GetBytes(sendMsg);
                sendArrMsg = new byte[arrMsg.Length + 1];

                // 设置标志位，代表更新用户信息
                sendArrMsg[0] = MSG_TO_ALL;
                Buffer.BlockCopy(arrMsg, 0, sendArrMsg, 1, arrMsg.Length);

                dictParagraph["AllUsers"].Add(newParagraph);
                chatRichTextBox.Document.Blocks.Add(newParagraph);
            }
            else
            {
                MsgHandler msgHandler = new MsgHandler(userData.userId, userData.userName, sendTo.userData.userId, inputTextBox.Text);

                string sendMsg = JsonConvert.SerializeObject(msgHandler);

                arrMsg = Encoding.UTF8.GetBytes(sendMsg);
                sendArrMsg = new byte[arrMsg.Length + 1];

                // 设置标志位，代表更新用户信息
                sendArrMsg[0] = MSG_TO_INDIVIDULAL;
                Buffer.BlockCopy(arrMsg, 0, sendArrMsg, 1, arrMsg.Length);

                dictParagraph[sendTo.userData.userId].Add(newParagraph);
                chatRichTextBox.Document.Blocks.Add(newParagraph);
            }

            try
            {
                socketClient.Send(sendArrMsg);
                inputTextBox.Text = "";
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

        #region ----------     文件发送相关     ----------
        /// <summary>
        ///     选择文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chooseFile_Click(object sender, RoutedEventArgs e)
        {
            //选择要发送的文件
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == true)
                fileTextBox.Text = ofd.FileName;
        }

        /// <summary>
        ///     发送文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sendFile_Click(object sender, RoutedEventArgs e)
        {
            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;

            //用文件流打开用户选择的文件
            try
            {
                using (FileStream fs = new FileStream(fileTextBox.Text, FileMode.Open))
                {
                    //定义一个4M的数组（缓冲区）
                    byte[] arrFile = new byte[1024 * 1024 * 2];
                    //用于发送真实数据的数组
                    byte[] arrFileSend = new byte[1];
                    //将文件数据读到数组arrFile中，并获取文件的真实长度
                    int length = fs.Read(arrFile, 0, arrFile.Length);

                    string fileStream = Encoding.UTF8.GetString(arrFile, 0, length);

                    int indexPoint = fileTextBox.Text.LastIndexOf('.');
                    int indexLine = fileTextBox.Text.LastIndexOf('\\');

                    string fileName = fileTextBox.Text.Substring(indexLine + 1, indexPoint - indexLine - 1);
                    string fileType = fileTextBox.Text.Substring(indexPoint + 1, fileTextBox.Text.Length - indexPoint - 1);

                    if (sendTo.nameAndId.Equals("所有人"))
                    {
                        FileHandler msgHandler = new FileHandler(userData.userId, userData.userName, fileName, fileType, "", fileStream);
                        string sendMsg = JsonConvert.SerializeObject(msgHandler);
                        byte[] arrTmp = Encoding.UTF8.GetBytes(sendMsg);
                        arrFileSend = new byte[arrTmp.Length + 1];
                        arrFileSend[0] = FILE_TO_ALL;
                        Buffer.BlockCopy(arrTmp, 0, arrFileSend, 1, arrTmp.Length);
                    }
                    else
                    {
                        FileHandler msgHandler = new FileHandler(userData.userId, userData.userName, fileName, fileType, sendTo.userData.userId, fileStream);
                        string sendMsg = JsonConvert.SerializeObject(msgHandler);
                        byte[] arrTmp = Encoding.UTF8.GetBytes(sendMsg);
                        arrFileSend = new byte[arrTmp.Length + 1];

                        arrFileSend[0] = FILE_TO_INDIVIDUAL;

                        Buffer.BlockCopy(arrTmp, 0, arrFileSend, 1, arrTmp.Length);
                    }

                    try
                    {
                        socketClient.Send(arrFileSend);
                        fileTextBox.Text = "";
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
            }
            catch
            {
                MessageBox.Show("请选择正确的文件路径！");
            }
        }
        #endregion
        
        #region ----------     聊天对象改变     ----------
        /// <summary>
        ///     发送消息的好友变了就改变聊天界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void onlineFriendlistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;
            chatFriendName.Content = sendTo.nameAndId;

            chatRichTextBox.Document.Blocks.Clear();
            if (sendTo.nameAndId.Equals("所有人"))
            {
                foreach (Paragraph p in dictParagraph["AllUsers"])
                    chatRichTextBox.Document.Blocks.Add(p);
            }
            else
            {
                foreach (Paragraph p in dictParagraph[sendTo.userData.userId])
                    chatRichTextBox.Document.Blocks.Add(p);
            }
        }
        #endregion

        #region ----------接收群发给所有人的消息----------
        /// <summary>
        ///     接收到群发给所有人的消息
        /// </summary>
        /// <param name="msgHandler"></param>
        private void ReceiveMsgForAll(MsgHandler msgHandler)
        {
            List<Paragraph> userParagraphList = dictParagraph["AllUsers"];

            

            Paragraph newParagraph = new Paragraph();

            Run run = new Run()
            {
                Text = msgHandler.fromName + "(" + msgHandler.from + ")" + "  " + System.DateTime.Now.ToString() + "\r\n",
                Foreground = new SolidColorBrush(Colors.Red)
            };
            newParagraph.Inlines.Add(run);

            InlineUIContainer inlineUIContainer = new InlineUIContainer()
            {

                Child = new TextBlock()
                {
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextWrapping = TextWrapping.Wrap,
                    Text = "  " + msgHandler.message + "\r\n"
                }
            };

            newParagraph.Inlines.Add(inlineUIContainer);

            userParagraphList.Add(newParagraph);

            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;


            if (sendTo.nameAndId.Equals("所有人"))
                chatRichTextBox.Document.Blocks.Add(newParagraph);
            else
            {
                onlineFriendType allUsers = (onlineFriendType)onlineFriendlistListBox.Items[0];
                allUsers.increaseNum();
            }
        }
        #endregion

        #region ----------接收发送个特定人的消息----------
        /// <summary>
        ///     接收到群发给所有人的消息
        /// </summary>
        /// <param name="msgHandler"></param>
        private void ReceiveMsgForIndividual(MsgHandler msgHandler)
        {
            List<Paragraph> userParagraphList = dictParagraph[msgHandler.from];

            Paragraph newParagraph = new Paragraph();


            Run run = new Run()
            {
                Text = msgHandler.fromName + "  " + System.DateTime.Now.ToString() + "\r\n",
                Foreground = new SolidColorBrush(Colors.Red)
            };
            newParagraph.Inlines.Add(run);

            InlineUIContainer inlineUIContainer = new InlineUIContainer()
            {

                Child = new TextBlock()
                {
                    Foreground = new SolidColorBrush(Colors.Black),
                    TextWrapping = TextWrapping.Wrap,
                    Text = "  " + msgHandler.message + "\r\n"
                }
            };

            newParagraph.Inlines.Add(inlineUIContainer);

            userParagraphList.Add(newParagraph);

            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;


            if (sendTo.nameAndId.Equals("所有人") == false && sendTo.userData.userId.Equals(msgHandler.from))
                chatRichTextBox.Document.Blocks.Add(newParagraph);
            else
            {
                int index;
                if (FindItemByUserId(msgHandler.from, out index))
                {
                    onlineFriendType user = (onlineFriendType)onlineFriendlistListBox.Items[index];
                    user.increaseNum();
                }
            }
        }
        #endregion

        #region ----------接受群发给所有人的文件----------
        /// <summary>
        ///     接受发送给所有人的文件
        /// </summary>
        /// <param name="msgHandler"></param>
        private void ReceiveFileForAll(FileHandler msgHandler)
        {
            byte[] fileStream = Encoding.UTF8.GetBytes(msgHandler.message);
            //保存文件对话框对象
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = msgHandler.fileType;
            if (msgHandler.fileType.Equals("txt"))
                sfd.Filter = "文本文件(*.txt)|*.txt";
            else if (msgHandler.fileType.Equals("doc"))
                sfd.Filter = "word文档(*.doc)|*.doc";
            else if (msgHandler.fileType.Equals("docx"))
                sfd.Filter = "word文档(*.docx)|*.docx";
            else if (msgHandler.fileType.Equals("cpp"))
                sfd.Filter = "c++文件(*.cpp)|*.cpp";
            else if (msgHandler.fileType.Equals("c"))
                sfd.Filter = "c文件(*.c)|*.c";
            else if (msgHandler.fileType.Equals("h"))
                sfd.Filter = "头文件(*.h)|*.h";
            else if (msgHandler.fileType.Equals("ppt"))
                sfd.Filter = "PowerPoint文件(*.ppt)|*.ppt";
            else if (msgHandler.fileType.Equals("pptx"))
                sfd.Filter = "PowerPoint文件(*.pptx)|*.pptx";
            else if (msgHandler.fileType.Equals("pdf"))
                sfd.Filter = "PDF文档(*.pdf)|*.pdf";
            else if (msgHandler.fileType.Equals("xls"))
                sfd.Filter = "excel文档(*.xls)|*.xls";
            if (sfd.Filter == null || sfd.Filter.Equals(""))
                sfd.Filter += "All FIles(*.*)|*.*";
            else
                sfd.Filter += "| All FIles(*.*)|*.*";

            List<Paragraph> userParagraphList = dictParagraph["AllUsers"];

            Paragraph newParagraph = new Paragraph();

            Run run = new Run()
            {
                Text = System.DateTime.Now.ToString() + " 收到来自" + msgHandler.fromName + "(" + msgHandler.from + ")" + " 的文件\r\n",
                Foreground = new SolidColorBrush(Colors.SteelBlue)
            };
            newParagraph.Inlines.Add(run);

            userParagraphList.Add(newParagraph);

            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;

            sfd.FileName = msgHandler.fileName + "." + msgHandler.fileType;
            if (sfd.ShowDialog(this) == true)
            {
                //获取文件将要保存的路径
                string fileSavePath = sfd.FileName;
                //创建文件流，让文件流根据路径创建一个文件
                using (FileStream fs = new FileStream(fileSavePath, FileMode.Create))
                {
                    Run mrun = new Run()
                    {
                        Text = "文件保存到"+fileSavePath+"\r\n",
                        Foreground = new SolidColorBrush(Colors.SteelBlue)
                    };
                    newParagraph.Inlines.Add(mrun);
                    fs.Write(fileStream, 1, fileStream.Length - 1);
                }
            }

            if (sendTo.nameAndId.Equals("所有人"))
                chatRichTextBox.Document.Blocks.Add(newParagraph);
            else
            {
                onlineFriendType allUsers = (onlineFriendType)onlineFriendlistListBox.Items[0];
                allUsers.increaseNum();
            }
        }
        #endregion

        #region ----------接收发送给特定人的文件----------
        /// <summary>
        ///     接受发送给特定人的文件
        /// </summary>
        /// <param name="msgHandler"></param>
        private void ReceiveFileForIndividual(FileHandler msgHandler)
        {
            byte[] fileStream = Encoding.UTF8.GetBytes(msgHandler.message);
            //保存文件对话框对象
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.DefaultExt = msgHandler.fileType;
            if (msgHandler.fileType.Equals("txt"))
                sfd.Filter = "文本文件(*.txt)|*.txt";
            else if (msgHandler.fileType.Equals("doc"))
                sfd.Filter = "word文档(*.doc)|*.doc";
            else if (msgHandler.fileType.Equals("docx"))
                sfd.Filter = "word文档(*.docx)|*.docx";
            else if (msgHandler.fileType.Equals("cpp"))
                sfd.Filter = "c++文件(*.cpp)|*.cpp";
            else if (msgHandler.fileType.Equals("c"))
                sfd.Filter = "c文件(*.c)|*.c";
            else if (msgHandler.fileType.Equals("h"))
                sfd.Filter = "头文件(*.h)|*.h";
            else if (msgHandler.fileType.Equals("ppt"))
                sfd.Filter = "PowerPoint文件(*.ppt)|*.ppt";
            else if (msgHandler.fileType.Equals("pptx"))
                sfd.Filter = "PowerPoint文件(*.pptx)|*.pptx";
            else if (msgHandler.fileType.Equals("pdf"))
                sfd.Filter = "PDF文档(*.pdf)|*.pdf";
            else if (msgHandler.fileType.Equals("xls"))
                sfd.Filter = "excel文档(*.xls)|*.xls";
            if (sfd.Filter == null || sfd.Filter.Equals(""))
                sfd.Filter += "All FIles(*.*)|*.*";
            else
                sfd.Filter += "| All FIles(*.*)|*.*";

            List<Paragraph> userParagraphList = dictParagraph[msgHandler.from];

            Paragraph newParagraph = new Paragraph();

            Run run = new Run()
            {
                Text = System.DateTime.Now.ToString() + " 收到来自" + msgHandler.fromName + "(" + msgHandler.from + ")" + " 的文件\r\n",
                Foreground = new SolidColorBrush(Colors.SteelBlue)
            };
            newParagraph.Inlines.Add(run);

            userParagraphList.Add(newParagraph);

            onlineFriendType sendTo = (onlineFriendType)onlineFriendlistListBox.SelectedItem;

            sfd.FileName = msgHandler.fileName + "." + msgHandler.fileType;
            if (sfd.ShowDialog(this) == true)
            {
                //获取文件将要保存的路径
                string fileSavePath = sfd.FileName;
                //创建文件流，让文件流根据路径创建一个文件
                using (FileStream fs = new FileStream(fileSavePath, FileMode.Create))
                {
                    Run mrun = new Run()
                    {
                        Text = "文件保存到" + fileSavePath + "\r\n",
                        Foreground = new SolidColorBrush(Colors.SteelBlue)
                    };
                    newParagraph.Inlines.Add(mrun);
                    fs.Write(fileStream, 1, fileStream.Length - 1);
                }
            }

            if (sendTo.nameAndId.Equals("所有人") == false && sendTo.userData.userId.Equals(msgHandler.from))
            {
                chatRichTextBox.Document.Blocks.Add(newParagraph);
            }               
            else
            {
                int index;
                if (FindItemByUserId(msgHandler.from, out index))
                {
                    onlineFriendType user = (onlineFriendType)onlineFriendlistListBox.Items[index];
                    user.increaseNum();
                }
            }
        }
        #endregion

        #region ---------- 从listbox中找到特定的在线好友 ----------
        /// <summary>
        ///     从listbox中找到相应userId的项
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="ans"></param>
        /// <returns></returns>
        private bool FindItemByUserId(string userId, out int index)
        {
            index = 0;
            foreach (object obj in onlineFriendlistListBox.Items)
            {
                onlineFriendType tmp = (onlineFriendType)obj;
                if (tmp.nameAndId.Equals("所有人") == false && tmp.userData.userId.Equals(userId))
                {
                    index = onlineFriendlistListBox.Items.IndexOf(obj);
                    return true;
                }
                    
            }
            return false;
        }
        #endregion

        #region ----------     增删好友相关     ----------
        /// <summary>
        ///     增加好友
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void subFriend_Click(object sender, RoutedEventArgs e)
        {
            addFriendFlag = false;
            subFriendFlag = true;
            confirmFriend.IsEnabled = true;
            friendTextBox.IsEnabled = true;
        }

        /// <summary>
        ///     删除好友
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void addFriend_Click(object sender, RoutedEventArgs e)
        {
            addFriendFlag = true;
            subFriendFlag = false;
            confirmFriend.IsEnabled = true;
            friendTextBox.IsEnabled = true;
        }

        private void confirmFriend_Click(object sender, RoutedEventArgs e)
        {
            string userId = friendTextBox.Text;
            if (userId == null || userId.Equals(""))
                MessageBox.Show("请输入正确的账号！");
            else
            {
                byte[] arrMsg = Encoding.UTF8.GetBytes(userId);
                byte[] sendArrMsg = new byte[arrMsg.Length + 1];
                if (addFriendFlag && !subFriendFlag)
                    sendArrMsg[0] = ADD_FRIEND;
                else if (!addFriendFlag && subFriendFlag)
                    sendArrMsg[0] = SUB_FRIEND;
                else
                {
                    MessageBox.Show("出错！");
                    return;
                }
                socketClient.Send(sendArrMsg);
            }

            addFriendFlag = subFriendFlag = false;
            friendTextBox.Text = "";
            friendTextBox.IsEnabled = false;
        }
        #endregion
    }

    #region ----------  显示好友列表的信息  ----------
    /// <summary>
    ///     显示好友列表的信息
    /// </summary>
    public class onlineFriendType
    {
        public string nameAndId { get; set; }
        public int msgNum { get; set; }
        public string msgNumString { get; set; }
        public UserData userData;

        public onlineFriendType()
        {

        }

        public onlineFriendType(UserData ud)
        {
            nameAndId = ud.userName + "(" + ud.userId + ")";
            msgNum = 0;
            msgNumString = "";
            userData = ud;
        }

        public onlineFriendType(string nAI)
        {
            nameAndId = nAI;
            msgNum = 0;
            msgNumString = "";
            userData = new UserData();
        }

        public void resetNum()
        {
            msgNum = 0;
            msgNumString = "";
        }

        public void increaseNum()
        {
            msgNum++;
            msgNumString = Convert.ToString(msgNum);
        }
    }
    #endregion

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
        public string fromName;
        public string to;
        public string message;

        public MsgHandler(string f, string fn, string t, string m)
        {
            from = f; fromName = fn; to = t; message = m;
        }
    };

    /// <summary>
    ///     用于JSON解析文件通信的结构体
    /// </summary>
    public struct FileHandler
    {
        public string from;
        public string fromName;
        public string to;
        public string fileName;
        public string fileType;
        public string message;

        public FileHandler(string f, string fn, string fiN, string ft, string t, string m)
        {
            from = f; fromName = fn; fileName = fiN; fileType = ft; to = t; message = m;
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
