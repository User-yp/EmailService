# EmailService

![CI](https://github.com/User-yp/EmailService/actions/workflows/ci.yml/badge.svg)

基于 .NET 8 构建的高性能邮件发送微服务，提供 REST API 与 gRPC 双协议支持，内置连接池管理、附件 FTP 存储、Redis 动态配置以及邮件状态追踪与重试机制。

---

## 特性概览

- **双协议接入** — 同时提供 RESTful HTTP API 与 gRPC（含流式上传），满足不同场景需求
- **Vue 3 前端控制台** — `Email.WebApp` 内嵌 Vue 3 SPA（运行时随项目提供，免 npm 构建），提供概览 / 邮件列表 / 详情 / 发信 / 依赖诊断页面
- **SMTP 连接管理** — 基于 MailKit 的复用连接（后台健康检测、空闲回收、自动重连），发送串行化以保证并发安全
- **邮件状态追踪** — 每封邮件对应一条 `EmailRecord`，记录发送状态、失败原因与重试次数
- **失败自动重试** — 内置后台调度服务，按「次数上限 + 冷却时间」扫描失败邮件并重投，同时支持手动重试
- **附件 FTP 归档** — 发送成功后把附件迁移至 FTP 服务器，按时间槽分目录存储并清空数据库中的二进制内容
- **Redis 动态配置** — SMTP、FTP 等运行参数存放在 Redis Hash 中，启动时加载，支持运行时刷新
- **自动 DI 注册** — 通过 `[Service]` 特征标记实现类，启动时自动扫描 "Email.*" 程序集并注入 IoC 容器
- **DDD 分层架构** — Domain / Infrastructure / Presentation 清晰分层，实体使用聚合根模式
- **软删除** — 聚合根统一置位删除标记，`SaveChanges` 会把物理删除改写为软删除，数据物理保留

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
│   │   ├── AggregateRoot.cs                  # 聚合根基类（Id、时间戳、软删除）
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
│   │   ├── RetrySettings.cs                  # 失败重试调度配置（appsettings）
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
│   ├── Scheduling/
│   │   └── EmailRetryBackgroundService.cs    # 失败邮件重试调度（后台服务）
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
├── Email.WebApi/                             # Web API 宿主
│   ├── Program.cs                             # 应用入口
│   ├── Controllers/
│   │   └── TestController.cs                 # 测试控制器
│   ├── appsettings.json                       # 静态配置文件
│   └── Properties/
│       └── launchSettings.json                # 启动配置
│
├── Email.WebApp/                             # Vue 3 前端控制台 + JSON 接口
│   ├── Program.cs                             # 应用入口（复用 InitService + SPA 回落）
│   ├── Controllers/                           # /api/dashboard|emails|attachments|diagnostics|seed
│   ├── Models/                                # 接口 DTO 与领域模型映射
│   └── wwwroot/                               # 前端静态资源
│       ├── index.html                         # SPA 外壳
│       ├── js/                                # api / ui / pages / app（免构建 Vue 3）
│       ├── css/app.css                        # 手写样式（不引 CDN）
│       └── vendor/                            # Vue 3.4 + Vue Router 4.4 运行时（MIT）
│
└── Email.Tests/                              # xunit 单元测试（不依赖外部服务）
    ├── Domain/                               # 重试策略、软删除、附件、分页等领域规则
    ├── Application/                          # DomainService 编排（内存替身）
    ├── Infrastructure/                       # DbContext 软删除、读模型查询、种子数据
    └── Fakes/                                # 手写测试替身（仓储 / SMTP / FTP）
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
dotnet run --project Email.WebApi     # REST API + gRPC
dotnet run --project Email.WebApp     # 前端控制台（http://localhost:5200）
```

启动后：
- REST API / Swagger：`http://localhost:5105/swagger`（有开发证书时同时监听 `https://localhost:7234/swagger`）
- gRPC 端点：`http://localhost:6102`（明文 HTTP/2，h2c）
- 前端控制台：`http://localhost:5200`（Vue 3 SPA：概览 `/`、邮件 `/emails`、发信 `/compose`、诊断 `/diagnostics`）

> 端口由 `Ports` 配置节控制（`Grpc` / `Http` / `Https`）。Kestrel 在代码里显式声明了监听端点，
> 因此 `launchSettings.json` 的 `applicationUrl` 与 `ASPNETCORE_URLS` 会被覆盖；
> 缺少开发证书时会打印告警并自动跳过 HTTPS 端口（执行 `dotnet dev-certs https` 可生成）。

---

## API 接口

### REST 端点（测试用）

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `/Test/Diagnostics` | 一次性检查 SMTP / FTP / SQL Server / Redis 连通性 |
| `GET` | `/Test/SmtpTest` | 单独测试 SMTP 连接与认证（不发送邮件） |
| `POST` | `/Test/SendEmail` | 发送邮件（JSON 请求体，无附件），返回入库后的完整状态 |
| `POST` | `/Test/SendEmailWithAttachments` | 发送带附件邮件（multipart/form-data，0~N 个附件） |
| `POST` | `/Test/SendRaw` | 不经数据库直接调用 SMTP，用于区分 SMTP 故障与数据库故障 |
| `GET` | `/Test/GetEmailStatus?emailId={id}` | 查询指定邮件的状态、附件与失败原因 |
| `GET` | `/Test/GetAllEmails?take={n}` | 按创建时间倒序列出最近邮件（默认 20，上限 200） |
| `GET` | `/Test/GetAttachment?attachmentId={id}&download={bool}` | 查询附件元数据；`download=true` 直接下载（已归档到 FTP 时自动回源） |
| `POST` | `/Test/Retry?emailId={id}` | 手动重试失败邮件（受次数与冷却时间约束） |
| `POST` | `/Test/Delete?emailId={id}` | 软删除邮件（连同附件与发送记录，数据物理保留） |
| `PUT` | `/Test/FtpUpload` | 上传文件到 FTP，返回远程路径 |
| `GET` | `/Test/FtpExists?path={path}` | 判断 FTP 文件是否存在 |
| `GET` | `/Test/FtpList?path={dir}` | 列出 FTP 目录下的文件名 |
| `GET` | `/Test/FtpDownload?path={path}` | 从 FTP 下载文件 |
| `DELETE` | `/Test/FtpDelete?path={path}` | 删除 FTP 上的文件 |

> 路由由 `[controller]/[action]` 模板生成，ASP.NET Core 默认会去掉 action 名末尾的 `Async`，
> 因此方法 `SendEmailAsync` 对应的实际路径是 `/Test/SendEmail`。
> TestController 仅用于开发联调（无认证、无业务约束），生产环境请替换为带认证与参数校验的业务控制器。

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

## 前端控制台（Email.WebApp，Vue 3）

`Email.WebApp` 是本项目的第三个表现层，与 `Email.WebApi` / `Email.RPCServe` 共用同一套领域与基础设施：
`Program.cs` 直接调用 `InitService`，Redis 动态配置、EF Core、SMTP / FTP、自动 DI 全部复用，没有第二套配置逻辑。

前端是 **Vue 3 单页应用**，后端只提供 JSON 接口，两者同源部署：

- `wwwroot/index.html` + `wwwroot/js/{api,ui,pages,app}.js` + `wwwroot/css/app.css`；
- Vue 3.4 与 Vue Router 4.4 运行时**内置在 `wwwroot/vendor`**（MIT，见 `vendor/README.md`），
  因此**不需要 npm install、不需要打包**，离线环境直接 `dotnet run` 即可使用；
- `Program.cs` 用 `MapFallbackToFile` 把前端路由回落到 `index.html`，刷新 `/emails/xxx` 不会 404。
- 没有浏览器也能校验模板：`node tools/check-vue-templates.js`（在 vm 沙箱里加载前端脚本并逐个编译组件模板）。

### 页面与路由

| 页面 | 路由 | 说明 |
|---|---|---|
| 概览 | `/` | 状态统计卡片、状态分布条、最近 8 封邮件、测试数据生成入口 |
| 邮件列表 | `/emails` | 状态筛选、关键字搜索（主题模糊 / 收件人完整地址）、分页（10 / 20 / 50），筛选条件同步到 URL，刷新与前进后退都保持 |
| 邮件详情 | `/emails/:id` | 基本信息、发送记录（失败原因 / 重试次数 / 失败地址）、HTML 预览（iframe sandbox）、附件下载、重试与软删除 |
| 发送测试邮件 | `/compose` | 收件人 / 抄送 / 密送、HTML 开关、多附件上传，走完整发送链路 |
| 依赖诊断 | `/diagnostics` | SMTP / FTP / 数据库 / Redis 四项连通性检测（与 WebApi 共用 `ISystemDiagnosticsService`） |

详情页的「重试发送」「软删除」直接调用 `IDomainService`，重试策略（次数上限、冷却时间）仍由领域层裁决。

### 后端接口

| 方法 | 路径 | 说明 |
|---|---|---|
| `GET` | `/api/dashboard` | 状态统计 + 最近邮件 |
| `GET` | `/api/emails` | 分页查询，参数 `status` / `keyword` / `page` / `pageSize` |
| `GET` | `/api/emails/{id}` | 邮件详情（含发送记录与附件） |
| `POST` | `/api/emails` | 发送邮件，`multipart/form-data`，字段 `to` / `cc` / `bcc` / `from` / `subject` / `body` / `isHtml` / `files` |
| `POST` | `/api/emails/{id}/retry` | 手动重试 |
| `DELETE` | `/api/emails/{id}` | 软删除 |
| `GET` | `/api/attachments/{id}` | 附件下载（已归档 FTP 时自动回源） |
| `GET` | `/api/diagnostics` | 依赖连通性 |
| `POST` | `/api/seed` | 生成测试数据，参数 `count` / `append` |

> 枚举统一按字符串序列化（`"Sent"` / `"Failed"` …），错误统一返回 `{ "error": "…" }`，
> 便于前端直接把消息显示到提示条里。

### 分层约定与扩展

- **表现层**：`Controllers`（`/api/*` JSON 接口）+ `wwwroot`（Vue SPA），只做编排与展示，不含业务规则。
- **应用层**：`IDomainService` / `ISystemDiagnosticsService` / `IDataSeeder` 定义在 `Email.Domain/IApplication`。
- **基础设施层**：`SystemDiagnosticsService`、`DataSeeder`、`EmailQueryComposer`、`EmailRepository` 负责查询与写库。
- 扩展新页面：`wwwroot/js/pages.js` 加页面组件 → `wwwroot/js/app.js` 注册路由 → 复用 `ui.js` 的共享组件与格式化函数。
- 想升级为 Vite + `.vue` 单文件组件：把构建产物输出到 `wwwroot` 即可，后端接口与回落配置无需改动。

### 生成测试数据

概览页底部可一键生成测试数据（默认 24 封，范围 1~200；勾选「追加」则忽略"已有数据"检查强制生成）：

- 直接写库、**不经过 SMTP**，不会真的发信；
- 覆盖已发送 / 失败 / 重试中 / 待发送四种状态，部分邮件带 1~2 个附件；
- 创建时间铺开到最近 14 天，列表与统计更接近真实场景；
- 收件人统一使用 `@example.com` 保留域名，清理时执行：

```sql
DELETE FROM dbo.EmailMessage WHERE [To] LIKE '%@example.com%';
```

> WebApp 的 `appsettings.json` 中 `RetrySettings:Enabled` 为 `false`，避免 WebApp 与 WebApi 同时运行两套失败重试调度。

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
       ├── 4. 成功 → MarkAsSent() → 附件归档到 FTP（FtpSettings.Enabled）
       ├── 5. 失败 → MarkAsFailed() + 记录错误详情（等待后台重试调度）
       └── 6. EmailRepository.UpdateAsync() 更新状态
```

### 重试机制

`EmailRecord` 负责策略判定，`EmailRetryBackgroundService` 负责调度执行：

- **领域规则**（`EmailRecord`）：
  - `CanRetry(maxRetryCount, cooldownPeriod)` — 仅当状态为 `Failed`/`Retry`、未达次数上限且已过冷却期时允许重试
  - `MarkForRetry(addresses, ...)` — 重试计数 +1，记录失败地址（自动去重）与错误详情
  - `MarkAsFailed` / `MarkForRetry` 写库前会按列长度截断错误信息，避免超长堆栈导致更新失败
- **后台调度**（`EmailRetryBackgroundService`）— 按 `RetrySettings.IntervalSeconds` 周期扫描 `Failed`/`Retry` 记录，逐封调用 `IDomainService.RetryEmailAsync` 重投
- **手动重试** — `POST /Test/RetryAsync?emailId=...`，与自动重试共用同一套策略
- 达到 `MaxRetryCount` 后不再自动重试，记录保持 `Failed`/`Retry` 状态供人工排查

---

## 配置说明

### 静态配置（appsettings.json）

| 节点 | 说明 |
|---|---|
| `RedisOption.ConnectionString` | Redis 连接字符串 |
| `RedisOption.DbNumber` | Redis 数据库编号 |
| `RedisOption.ConfigKey` | Redis Hash Key（存放动态配置） |
| `ConnectionOption.ConnectionString` | SQL Server 连接字符串 |
| `RetrySettings.*` | 失败邮件重试调度参数（见下表） |
| `Ports.Grpc` / `Ports.Http` / `Ports.Https` | gRPC（h2c）、REST、HTTPS 监听端口；`Ports.Https` 设为 `0` 可关闭 |

#### 重试调度参数（RetrySettings）

| 字段 | 默认值 | 说明 |
|---|---|---|
| `Enabled` | `true` | 是否启用后台重试调度 |
| `IntervalSeconds` | `60` | 扫描间隔（秒），最小 5 秒 |
| `MaxRetryCount` | `3` | 单封邮件最大重试次数 |
| `CooldownMinutes` | `5` | 两次重试之间的冷却时间（分钟） |
| `BatchSize` | `20` | 单轮最多处理的邮件数量 |

### 动态配置（Redis Hash → EmailConfig）

| 字段 | 类 | 关键属性 |
|---|---|---|
| `SmtpSettings` | `SmtpSettings` | Server, Port, SenderEmail, Username, Password, UseSsl, MonitorInterval, InactivityTimeout |
| `FtpSettings` | `FtpSettings` | Host, Port, Username, Password, RootPath, UsePassiveMode, EnableSsl, Enabled（是否归档附件到 FTP，默认 `true`） |

> 动态配置在启动时通过 `DllExtension.LoadConfiguration()` 加载。如需运行时刷新，可扩展该机制加入定时轮询。

> `RetrySettings` 是唯一走 appsettings.json 的配置节（未标记 `[Option]`），不需要写入 Redis Hash。

#### 配置加载顺序与 fail-fast

1. 先绑定 appsettings.json（以及环境变量）中存在的 `[Option]` 配置节，例如 `ConnectionOption`；
2. 再读取 Redis Hash（`RedisOption:ConfigKey`）中的同名字段，**同名项以 Redis 为准**；
3. Redis 不可用时打印告警并继续使用静态配置；
4. `ConnectionOption` / `SmtpSettings` / `FtpSettings` 三者只要有一个取不到，**启动即失败并给出明确提示**，不再出现"启动成功、请求时才报错"。

---

## 测试

### 运行

```bash
dotnet test                                               # 运行解决方案中的全部测试
dotnet test Email.Tests                                   # 只跑单元测试项目
dotnet test --filter FullyQualifiedName~EmailRecordTests  # 按测试类筛选
```

### 覆盖范围（77 个用例）

| 测试类 | 覆盖内容 |
|---|---|
| `EmailRecordTests` | 重试策略：次数上限、冷却时间、状态流转、失败地址去重、错误信息按列长截断 |
| `EmailMessageTests` | 工厂方法校验、附件关联、`MarkAsSent/Failed/Retry` 委托、软删除级联 |
| `AttachmentTests` | `FileSize` 按字节存储、FTP 归档后清空数据库内容、空路径不误判为已归档 |
| `EmailDbContextSoftDeleteTests` | `Remove` 被改写为软删除、全局查询过滤器生效、级联软删除、附件字段落库（EF InMemory） |
| `DomainServiceTests` | 发送编排（入库→发送→标记状态→附件归档）、失败仍落库、FTP 归档失败不影响发送、重试策略、软删除委托 |
| `PagedResultTests` | 分页模型：总页数向上取整、上下页判定、页码与页大小归一化 |
| `EmailQueryComposerTests` | 读模型查询必须能下推 SQL：用 `ToQueryString()` 校验 LIKE / OPENJSON / OFFSET / GROUP BY，且不把附件二进制拉回来 |
| `EmailRepositoryQueryTests` | 分页与排序、状态筛选、关键字匹配、页大小钳制、统计聚合与成功率（EF InMemory） |
| `DataSeederTests` | 测试数据的状态分布、创建时间铺开、附件字节数、已有数据时跳过、append 追加、数量钳制 |

### 设计取舍

- **不依赖外部服务**：SQL Server 用 EF Core InMemory 提供程序，SMTP / FTP / Redis 用手写替身（`Email.Tests/Fakes`），因此离线和 CI 环境都能直接跑完。
- **手写替身而非 Mock 框架**：接口成员不多，手写替身让"调用了几次、传了什么参数"一目了然，也少一个依赖。
- **不引入假时钟**：冷却期相关行为通过把 `cooldownPeriod` 分别传 `TimeSpan.Zero` 和较大值来验证两个分支。

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
6. **gRPC 传输安全**：内置的 6102 端口是明文 h2c，仅供内网/开发使用；生产建议由网关或反向代理终结 TLS

---

## 许可证

本项目为个人开源项目。

---

## 作者

**pengye** — [GitHub](https://github.com/User-yp)

---

> If you find this project helpful, please give it a ⭐ star!
