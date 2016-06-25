using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UChatServer
{
    class DatabaseHandler
    {
        // 数据库连接
        private SqlConnection con;
        private SqlCommand com;

        public DatabaseHandler()
        {
            try
            {
                con = new SqlConnection("server=127.0.0.1;database=UChat;uid=sa;pwd=123456");
                con.Open();
            }
            catch (Exception e)
            {
                Console.WriteLine("{0]", e.Message);
            }
        }

        #region ----------   获取注册的新id   ----------
        /// <summary>
        ///     为注册用户获取一个新的id
        /// </summary>
        /// <returns>返回注册的id</returns>
        public string GetNewId()
        {
            string id = "";
            Random rnd = new Random();

            // 初始化查询语句
            com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            
            // 随机生成一个id，检查数据库中是否含有此id，直到生成一个未注册的id返回
            while (true)
            {
                for (int i = 0; i < 8; i++)
                    id = rnd.Next(0, 10).ToString() + id;

                com.CommandText = "select * from UserInfo where id = '" + id + "'";

                // 获得查询结果
                SqlDataReader ans = com.ExecuteReader();

                if (!ans.Read())
                    break;
            }

            return id;
        }
        #endregion

        #region ----------        登录        ----------
        /// <summary>
        ///     登录
        /// </summary>
        /// <param name="userId">用户名</param>
        /// <param name="userPassword">密码</param>
        /// <param name="userInfo">如果登陆成功返回信息为用户的个人信息</param>
        /// <returns>返回登录是否成功</returns>
        public bool Login(string userId, string userPassword, out UserData userData)
        {
            userData = new UserData();

            // 初始化查询语句
            com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = "select id, code, name, gender, age from UserInfo where id = '" + userId + "'";

            // 获得查询结果
            SqlDataReader ans = com.ExecuteReader();

            if (ans.Read())
            {
                if (Convert.ToString(ans[1]).Trim().Equals(userPassword))
                {
                    userData = new UserData((string)ans[0], (string)ans[2], (string)ans[3], (int)ans[4]);                   
                    return true;
                }
                else
                    return false;
            }
            return false;
        }
        #endregion

        #region ----------      用户注册      ----------
        /// <summary>
        ///     用户注册
        /// </summary>
        /// <param name="userId">UChat账号</param>
        /// <param name="userName">用户昵称</param>
        /// <param name="userPassword">用户密码</param>
        /// <param name="userGender">性别</param>
        /// <param name="userAge">年龄</param>
        /// <returns></returns>
        public bool Register(string userId, string userName, string userPassword, string userGender, int userAge)
        {
            if (userGender == null || userGender == "")
                userGender = "男";

            // 初始化查询语句
            com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = "insert into UserInfo(id, name, code, gender, age) values ('" + userId + "', '" + userName + "', '" + userPassword + "', '" + userGender + "', '" + userAge +"')";

            com.ExecuteNonQuery();

            Console.WriteLine("新用户\"{0}:{1}:{2}:{3}:{4}\"注册成功", userId, userName, userPassword, userGender, userAge);

            return false;
        }
        #endregion

        #region ----------  查询用户个人信息  ----------
        /// <summary>
        ///     查询某个人的数据
        /// </summary>
        /// <param name="queryUserId">查询信息的用户id</param>
        /// <returns>返回结果串</returns>
        public UserData QueryUserData(string queryUserId)
        {
            // 初始化查询语句
            com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = "select id, name, gender, age from UserInfo wherr id = '" + queryUserId + "'";

            // 获得查询结果
            SqlDataReader ans =  com.ExecuteReader();

            UserData userData = new UserData();

            // 保存信息
            if (ans.Read())
                userData = new UserData((string)ans[0], (string)ans[1], (string)ans[2], (int)ans[3]);

            return userData;
        }
        #endregion

        #region ---------- 查询某个用户的好友 ----------
        /// <summary>
        ///     查询某个用户的好友
        /// </summary>
        /// <param name="userId">用户id</param>
        /// <returns>好友信息列表</returns>
        public List<UserData> FindFriendOfSomeone(string userId)
        {
            List<UserData> ans = new List<UserData>();

            // 初始化查询语句
            com = new SqlCommand();
            com.Connection = con;
            com.CommandType = CommandType.Text;
            com.CommandText = "select id, name, gender, age from Friendship, UserInfo where idA = '" + userId + "' and id = idB";

            // 获得查询结果
            SqlDataReader rst = com.ExecuteReader();

            while(rst.Read())
            {
                UserData userData = new UserData((string)rst[0], (string)rst[1], (string)rst[2], (int)rst[3]);
                ans.Add(userData);
            }

            com.CommandText = "select id, name, gender, age from Friendship, UserInfo where idB = '" + userId + "' and id = idA";

            rst = com.ExecuteReader();

            while (rst.Read())
            {
                UserData userData = new UserData((string)rst[0], (string)rst[1], (string)rst[2], (int)rst[3]);
                ans.Add(userData);
            }

            return ans;
        }
        #endregion
    }
}
