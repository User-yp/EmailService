# EmailService

基于 .NET 8 构建的高性能邮件发送微服务，提供 REST API 与 gRPC 双协议支持，内置连接池管理、附件 FTP 存储、Redis 动态配置以及邮件状态追踪与重试机制。

---

## 特性概览

- **双协议接入** — 同时提供 RESTful HTTP API 与 gRPC（含流式上传），满足不同场景需求
- **SMTP 连接池** — 基于 MailKit 的单例 SMTP 连接管理，支持后台健康检测、空闲回收与自动重连
- **邮件状态追踪** — 每封邮件对应一条 `EmailRecord`，记录发送状态、失败原因与重试次数，支持自定义重试策略
- **附件 FTP 存储** — 附件可由数据库迁移至 FTP 服务器，按时间槽分目录存储，减少数据库压力
- **Redis 动态配置** — SMTP、FTP 等运行参数存放在 Redis Hash 中，启动时加载，支持运行时刷新
- **自动 DI 注册** — 通过 `[Service]` 特征标记实现类，启动时自动扫描 "Email.*" 程序集并注入 IoC 容器
- **DDD 分层架构** — Domain / Infrastructure / Presentation 清晰分层，实体使用聚合根模式
- **软删除** — 所有实体默认启用全局查询过滤器，数据物理保留

---

## 技术栈

| 层级 | 技术 / 组件 |
|---|---|
| 运行时 | .NET 8 |
| Web 框架 | ASP.NET Core Web API |
| gRPC | Grpc.AspNetCore + Google.Protobuf |
| ORM | Entity Framework Core 8（SQL Server） |
| SMTP 客户端 | MailKit |
| FTP 客户端 | FluentFTP |
| 缓存 / 配置 | StackExchange.Redis |
| JSON 序列化 | Newtonsoft.Json |
| API 文档 | Swashbuckle（Swagger） |

---

## 项目结构

```
EmailService/
├── EmailService.sln                          # 解决方案文件
├── README.md                                 # 本文档
│
├── Email.Domain/                             # 领域层（实体、接口、枚举）
│   ├── Common.cs                             # EmailStatus 枚举
│   ├── Entity/
│   │   ├── IAggregateRoot.cs                 # 聚合根基类（Id、时间戳、软删除）
│   │   ├── EmailMessage.cs                   # 邮件消息实体（partial）
│   │   ├── EmailRecord.cs                    # 邮件发送记录实体
│   │   └── Attachment.cs                     # 附件实体（partial）
│   ├── IApplication/
│   │   └── IDomainService.cs                 # 应用服务接口
│   ├── IRepository/
│   │   ├── IEmailHandler.cs                  # SMTP 发送处理接口
│   │   ├── IEmailRepository.cs               # 邮件仓储接口
│   │   └── IFtpService.cs                    # FTP 服务接口
│   └── Partial/
│       ├── EmailMessage.cs                   # EmailMessage 补充逻辑
│       └── Attachment.cs                     # Attachment 补充逻辑
│
├── Email.Extension/                          # 共享工具层
│   ├── Attributes/
│   │   ├── OptionAttribute.cs                # [Option] 标记（Redis 配置类）
│   │   └── ServiceAttribute.cs               # [Service] 标记（自动 DI 注册）
│   ├── Option/
│   │   ├── ConnectionOption.cs               # SQL Server 连接配置
│   │   ├── FtpSettings.cs                    # FTP 服务器配置
│   │   ├── RedisOption.cs                    # Redis 连接配置
│   │   └── SmtpSettings.cs                   # SMTP 服务器配置
│   └── DllExtension.cs                       # 启动扩展：配置加载 + 自动 DI
│
├── Email.Infrastructure/                     # 基础设施层（数据访问、外部服务）
│   ├── EmailDbContext.cs                     # EF Core 数据上下文
│   ├── RegistExtension.cs                    # IServiceCollection 初始化扩展
│   ├── Application/
│   │   └── DomainService.cs                  # 邮件发送核心编排服务
│   ├── Config/
│   │   ├── AttachmentConfig.cs               # Attachment 实体 Fluent 配置
│   │   ├── EmailMessageConfig.cs             # EmailMessage 实体 Fluent 配置
│   │   └── EmailRecordConfig.cs              # EmailRecord 实体 Fluent 配置
│   ├── Factory/
│   │   ├── EmailDbContextFactory.cs           # DbContext 工厂（Design-time）
│   │   └── SmtpClientFactory.cs              # SMTP 客户端工厂（单例，连接池）
│   ├── Migrations/                            # EF Core 数据库迁移
│   └── Repository/
│       ├── EmailHandler.cs                   # SMTP 发送实现
│       ├── EmailRepository.cs                # 邮件仓储实现
│       └── FtpService.cs                     # FTP 服务实现（AsyncLazy 初始化）
│
├── Email.RPCServe/                           # gRPC 服务层
│   ├── Protos/
│   │   └── email_service.proto               # gRPC 服务定义
│   ├── GrpcEmailService.cs                   # gRPC 服务实现（含流式上传）
│   └── RPCExtension.cs                       # gRPC 注册扩展
│
└── Email.WebApi/                             # Web API 宿主
    ├── Program.cs                             # 应用入口
    ├── Controllers/
    │   └── TestController.cs                 # 测试控制器
    ├── appsettings.json                       # 静态配置文件
    └── Properties/
        └── launchSettings.json                # 启动配置
```

---

## 快速开始

### 环境要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server（本地或远程）
- Redis（本地或远程）
- （可选）FTP 服务器，用于附件远程存储

### 1. 克隆仓库

```bash
git clone https://github.com/User-yp/EmailService
cd EmailService
```

### 2. 配置 appsettings.json

编辑 `Email.WebApi/appsettings.json`，填写 SQL Server 与 Redis 连接信息：

```json
{
  "RedisOption": {
    "ConnectionString": "127.0.0.1:6379",
    "DbNumber": 0,
    "ConfigKey": "EmailConfig"
  },
  "ConnectionOption": {
    "ConnectionString": "Server=.;Database=emailserve;User Id=sa;Password=你的密码;TrustServerCertificate=true;"
  }
}
```

### 3. 设置 Redis 动态配置

在 Redis 中创建 Hash，Key 为 `EmailConfig`（对应上述 `ConfigKey`），写入以下字段：

| Hash Field | 示例值 |
|---|---|
| `SmtpSettings` | `{"Server":"smtp.example.com","Port":587,"SenderName":"Your Service","SenderEmail":"noreply@example.com","Username":"your-username","Password":"your-password","UseSsl":true,"Timeout":30000,"MonitorInterval":60,"InactivityTimeout":300}` |
| `FtpSettings` | `{"Host":"ftp.example.com","Port":21,"Username":"ftp-user","Password":"ftp-password","RootPath":"/emails","UsePassiveMode":true,"EnableSsl":false,"Timeout":30000}` |

> **注意**：`SmtpSettings` 和 `FtpSettings` 必须作为 JSON 字符串存储在 Redis Hash 中。项目使用 Newtonsoft.Json 反序列化，支持驼峰命名。

### 4. 执行数据库迁移

```bash
dotnet ef database update --project Email.Infrastructure --startup-project Email.WebApi
```

### 5. 启动服务

```bash
dotnet run --project Email.WebApi
```

启动后：
- REST API Swagger 页面：`https://localhost:7234/swagger`（开发环境自动打开）
- gRPC 端点：`https://localhost:6102`（HTTP/2）

---

## API 接口

### REST 端点（测试用）

| 方法 | 路径 | 说明 |
|---|---|---|
| `POST` | `/Test/SendEmailAsync` | 发送纯文本 / HTML 邮件（参数硬编码） |
| `POST` | `/Test/AttachmentAsync` | 发送带单个附件的邮件 |
| `POST` | `/Test/AttachmentsAsync` | 发送带多个附件的邮件 |
| `PUT` | `/Test/FTPTestAsync` | 测试 FTP 文件上传 |

> TestController 中参数为硬编码的测试数据，实际使用时需替换为业务控制器。

### gRPC 服务

```protobuf
service EmailService {
  // 普通发送（支持多附件）
  rpc SendEmail (EmailRequest) returns (EmailResponse);

  // 流式发送（适用于大附件，客户端分块上传）
  rpc SendEmailWithAttachment (stream AttachmentChunk) returns (EmailResponse);
}
```

#### 流式发送协议

客户端先发送一个包含 `EmailRequest` 的 `Metadata` 消息，再依次发送 `FileChunk` 数据块。服务端将所有块拼接为完整附件后执行发送。

```
Metadata → Chunk₁ → Chunk₂ → ... → Chunkₙ → 服务器返回 EmailResponse
```

---

## 架构设计

### 分层架构

```
┌─────────────────────────────────────┐
│        Email.WebApi (宿主)           │  ← ASP.NET Core 启动、中间件、路由
│         Email.RPCServe (gRPC)        │  ← gRPC 服务实现、流式处理
├─────────────────────────────────────┤
│        Email.Infrastructure          │  ← 业务编排、EF Core、SMTP、FTP
├─────────────────────────────────────┤
│        Email.Domain (领域)            │  ← 实体、接口、枚举
├─────────────────────────────────────┤
│        Email.Extension (工具)         │  ← 特征、配置 POCO、启动扩展
└─────────────────────────────────────┘
```

### 邮件发送流程

```
HTTP / gRPC 请求
       │
       ▼
  DomainService.SendEmailAsync()
       │
       ├── 1. 创建 EmailMessage + EmailRecord
       ├── 2. EmailRepository.AddAsync() 持久化
       ├── 3. EmailHandler.SendEmailAsync() 发送
       │         │
       │         ├── 构建 MimeKit.MimeMessage
       │         ├── SmtpClientFactory.GetConnectedClientAsync()
       │         └── SmtpClient.SendAsync()
       │
       ├── 4. 成功 → MarkAsSent()
       ├── 5. 失败 → MarkAsFailed() + 记录错误详情
       └── 6. EmailRepository.UpdateAsync() 更新状态
```

### 重试机制

`EmailRecord` 实体内置重试逻辑：

- `MaxRetryCount`：默认 3 次
- `CooldownPeriod`：默认 5 分钟冷却
- `CanRetry()`：检查是否达到最大次数且已过冷却期
- `MarkForRetry(addresses)`：标记重试并记录失败地址

---

## 配置说明

### 静态配置（appsettings.json）

| 节点 | 说明 |
|---|---|
| `RedisOption.ConnectionString` | Redis 连接字符串 |
| `RedisOption.DbNumber` | Redis 数据库编号 |
| `RedisOption.ConfigKey` | Redis Hash Key（存放动态配置） |
| `ConnectionOption.ConnectionString` | SQL Server 连接字符串 |

### 动态配置（Redis Hash → EmailConfig）

| 字段 | 类 | 关键属性 |
|---|---|---|
| `SmtpSettings` | `SmtpSettings` | Server, Port, SenderEmail, Username, Password, UseSsl, MonitorInterval, InactivityTimeout |
| `FtpSettings` | `FtpSettings` | Host, Port, Username, Password, RootPath, UsePassiveMode, EnableSsl |

> 动态配置在启动时通过 `DllExtension.LoadConfiguration()` 加载。如需运行时刷新，可扩展该机制加入定时轮询。

---

## 扩展指南

### 添加新的邮件配置类型

1. 在 `Email.Extension/Option/` 下创建 POCO 类，添加 `[Option]` 特征
2. 在 Redis `EmailConfig` Hash 中加入同名 JSON 字段
3. 启动时自动加载并注册为 Singleton

### 添加新的业务服务

1. 定义接口（放置于 `Email.Domain`）
2. 实现类添加 `[Service(ServiceLifetime.Scoped/Singleton/Transient)]` 特征
3. 确保接口名称包含实现类名称（如 `IMyService` ↔ `MyService`）
4. 启动时自动注册至 IoC 容器

### 生产环境部署建议

1. **移除 TestController**：替换为带认证授权、参数校验的业务控制器
2. **HTTPS 证书**：生产环境建议使用正式 TLS 证书并启用 HTTPS 重定向
3. **日志与监控**：接入 Serilog / OpenTelemetry 等日志与 APM 方案
4. **健康检查**：添加 `/healthz` 端点检查 SQL Server、Redis、SMTP、FTP 连通性
5. **连接字符串安全**：使用 User Secrets、环境变量或 Azure Key Vault 管理敏感信息

---

## 许可证

本项目为个人开源项目。

---

## 作者

**pengye** — [GitHub](https://github.com/User-yp)

---

> If you find this project helpful, please give it a ⭐ star!
