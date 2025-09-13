namespace Email.Domain;
// 邮件状态枚举
public enum EmailStatus
{
    Draft,      // 草稿
    Sent,       // 已发送
    Failed,      // 发送失败
    Init,        // 初始化
    Retry        // 重试中
}