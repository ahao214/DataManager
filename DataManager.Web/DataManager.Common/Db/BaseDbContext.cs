using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using Microsoft.Extensions.Configuration;
using SqlSugar;

namespace DataManager.Common.Db
{
    /// <summary>
    /// 数据库操作基类
    /// </summary>
    public class BaseDbContext
    {
        public SqlSugarClient Db;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BaseDbContext(IConfiguration configuration)
        {
            try
            {
                //主库
                var connMain = ConfigurationManager.AppSettings["ConnMain"];
                //从库
                var connFrom = ConfigurationManager.AppSettings["ConnFrom"];
                InitDataBase(connFrom == null
                    ? new List<string> {connMain.ToString()}
                    : new List<string> {connMain.ToString(), connFrom.ToString()});
            }
            catch (Exception ex)
            {
                throw new Exception("未配置数据库连接字符串");
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="listConnSettings">
        /// 连接字符串配置Key集合,配置多个连接则是读写分离 
        /// </param>
        public BaseDbContext(List<string> listConnSettings)
        {
            try
            {
                var listConn = new List<string>();
                foreach (var t in listConnSettings)
                {
                    listConn.Add(ConfigurationManager.ConnectionStrings[t].ToString());
                }

                InitDataBase(listConn);
            }
            catch
            {
                throw new Exception("未配置数据库连接字符串");
            }

        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverIp">服务器IP</param>
        /// <param name="user">用户名</param>
        /// <param name="pass">密码</param>
        /// <param name="dataBase">数据库</param>
        public BaseDbContext(string serverIp, string user, string pass, string dataBase)
        {
            InitDataBase(new List<string>
                {$"server={serverIp};user id={user};password={pass};persistsecurityinfo=True;database={dataBase}"});
        }

        /// <summary>
        /// 初始化数据库连接
        /// </summary>
        /// <param name="listConn">连接字符串</param>
        private void InitDataBase(List<string> listConn)
        {
            var connStr = ""; //主库
            var slaveConnectionConfigs = new List<SlaveConnectionConfig>(); //从库集合
            for (var i = 0; i < listConn.Count; i++)
            {
                if (i == 0)
                {
                    connStr = listConn[i]; //主数据库连接
                }
                else
                {
                    slaveConnectionConfigs.Add(new SlaveConnectionConfig()
                    {
                        HitRate = i * 2,
                        ConnectionString = listConn[i]
                    });
                }
            }

            //如果配置了 SlaveConnectionConfigs那就是主从模式,所有的写入删除更新都走主库，查询走从库，
            //事务内都走主库，HitRate表示权重 值越大执行的次数越高，如果想停掉哪个连接可以把HitRate设为0
            var ctx = new ConfigureExternalServices();

            Db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = connStr,
                DbType = DbType.SqlServer,
                IsAutoCloseConnection = true,
                SlaveConnectionConfigs = slaveConnectionConfigs
            });
            Db.Ado.CommandTimeOut = 30000; //设置超时时间
            Db.Aop.OnLogExecuted = (sql, pars) => //SQL执行完事件
            {
            };
            Db.Aop.OnLogExecuting = (sql, pars) => //SQL执行前事件
            {
            };
            Db.Aop.OnError = (exp) => //执行SQL 错误事件
            {
                throw new Exception("出错SQL：" + exp.Sql + "\r\n" + exp.Message);
            };
            Db.Aop.OnExecutingChangeSql = (sql, pars) => //SQL执行前 可以修改SQL
            {
                return new KeyValuePair<string, SugarParameter[]>(sql, pars);
            };
        }

        public SqlSugarClient GetClient() => Db;
    }
}
