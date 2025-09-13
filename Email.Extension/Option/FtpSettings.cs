using Email.Extension.Attributes;

namespace Email.Extension.Option;
[Option]
public class FtpSettings
{
    public string Host { get; set; }
    public int Port { get; set; } = 21;
    public string Username { get; set; }
    public string Password { get; set; }
    public string RootPath { get; set; } = "/";
    public bool UsePassiveMode { get; set; } = true;
    public bool EnableSsl { get; set; } = false;
    public int Timeout { get; set; } = 30000;
    public FtpSettings()
    {

    }

    public FtpSettings(string host, int port, string username, string password, string rootPath, bool usePassiveMode, bool enableSsl, int timeout)
    {
        Host = host;
        Port = port;
        Username = username;
        Password = password;
        RootPath = rootPath;
        UsePassiveMode = usePassiveMode;
        EnableSsl = enableSsl;
        Timeout = timeout;
    }
}