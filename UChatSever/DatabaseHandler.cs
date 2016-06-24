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
            com.CommandText = "select id, code, name, gender, age from UserInfo wherr id = '" + userId + "'";

            // 获得查询结果
            SqlDataReader ans = com.ExecuteReader();


            if (ans.Read())
            {
                if (Convert.ToString(ans[1]).Trim().Equals(userPassword))
                {
                    userData = new UserData((string)ans[0], (string)ans[2], (string)ans[3], (string)ans[4]);                   
                    return true;
                }
                else
                    return false;
            }
            return false;
        }
        #endregion

        #region ----------  查询用户个人信息  ----------
        /// <summary>
        ///     查询某个人的数据
        /// </summary>
        /// <param name="queryUserId">查询信息的用户id</param>
        /// <returns>返回结果串</returns>
        public string QueryUserData(string queryUserId)
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
                userData = new UserData((string)ans[0], (string)ans[1], (string)ans[2], (string)ans[3]);

            return JsonConvert.SerializeObject(userData);
        }
        #endregion
    }
}
