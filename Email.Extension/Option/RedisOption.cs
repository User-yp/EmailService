using Email.Extension.Attributes;

namespace Email.Extension.Option;

[Option]
public class RedisOption
{
    //redis链接
    public string ConnectionString { get; set; }

    //数据库
    public int DbNumber { get; set; }

    //配置键
    public string ConfigKey { get; set; }
    public RedisOption() { }

    public RedisOption(string connectionString, int dbNumber, string configKey)
    {
        ConnectionString = connectionString;
        DbNumber = dbNumber;
        ConfigKey = configKey;
    }
}