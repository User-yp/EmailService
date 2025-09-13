using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Email.Extension.Option;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Email.Infrastructure.Factory;

public class EmailDbContextFactory : IDesignTimeDbContextFactory<EmailDbContext>
{
    private readonly ConnectionOption connection;

    public EmailDbContextFactory(ConnectionOption connection)
    {
        this.connection = connection;
    }
    public EmailDbContext CreateDbContext(string[] args)
    {
        // 设计时连接字符串（迁移时使用）
        /*var connectionString = "Server=.;Database=emailserve;User Id=sa;Password=1234;TrustServerCertificate=true;";*/

        var optionsBuilder = new DbContextOptionsBuilder<EmailDbContext>();
        optionsBuilder.UseSqlServer(connection.ConnectionString);

        return new EmailDbContext(optionsBuilder.Options);
    }
}
