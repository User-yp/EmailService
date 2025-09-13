using Email.Extension.Attributes;

namespace Email.Extension.Option;
[Option]
public class ConnectionOption
{
    public string ConnectionString { get; set; }
    public ConnectionOption()
    {

    }

    public ConnectionOption(string connectionString)
    {
        ConnectionString = connectionString;
    }
}