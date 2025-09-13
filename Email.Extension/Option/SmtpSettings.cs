using Email.Extension.Attributes;

namespace Email.Extension.Option;

[Option]
public class SmtpSettings
{
    public string Server { get; set; }
    public int Port { get; set; }
    public string SenderName { get; set; }
    public string SenderEmail { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public bool UseSsl { get; set; }
    public int Timeout { get; set; }
    public int MonitorInterval { get; set; }
    public int InactivityTimeout { get; set; }
    public SmtpSettings()
    {

    }

    public SmtpSettings(string server, int port, string senderName, string senderEmail, string username, string password, bool useSsl, int timeout = 0, int inactivityTimeout = 0, int monitorInterval = 0)
    {
        Server = server;
        Port = port;
        SenderName = senderName;
        SenderEmail = senderEmail;
        Username = username;
        Password = password;
        UseSsl = useSsl;
        Timeout = timeout;
        InactivityTimeout = inactivityTimeout;
        MonitorInterval = monitorInterval;
    }
}