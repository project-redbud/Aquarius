using System.Net;
using System.Net.Mail;
using Aquarius.Api.Models;

namespace Aquarius.Api.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>从 SiteSettings 构建 SmtpClient（如未配置则返回 null）</summary>
    private SmtpClient? BuildClient(SiteSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.SmtpHost)) return null;

        var client = new SmtpClient(s.SmtpHost, s.SmtpPort)
        {
            EnableSsl = s.SmtpEnableSsl,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(s.SmtpUser)
                ? null
                : new NetworkCredential(s.SmtpUser, s.SmtpPassword)
        };
        return client;
    }

    /// <summary>发送邮件（后台任务，不阻塞）。如 SMTP 未配置则静默跳过。</summary>
    public void SendBackground(SiteSettings settings, string to, string subject, string body)
    {
        if (!settings.SmtpEnabled)
        {
            Console.WriteLine($"[Email] SMTP disabled. Would send to {to}: {subject}");
            return;
        }
        Console.WriteLine($"[Email] Queuing background send to {to}: {subject}");
        _ = Task.Run(async () =>
        {
            using var client = BuildClient(settings);
            if (client == null)
            {
                Console.WriteLine($"[Email] SMTP not configured. Would send to {to}: {subject}");
                return;
            }

            var siteName = string.IsNullOrWhiteSpace(settings.SiteName) ? "Aquarius" : settings.SiteName;
            var displayName = siteName;
            var fromAddr = new MailAddress(settings.SmtpUser, displayName);
            var fullBody = body + $"<hr style=\"border:none;border-top:1px solid #e5e7eb;margin:16px 0\"><p style=\"color:#9ca3af;font-size:12px\">{siteName} 运营团队</p>";
            using var msg = new MailMessage(fromAddr, new MailAddress(to))
            {
                Subject = subject,
                Body = fullBody,
                IsBodyHtml = true,
                Sender = new MailAddress(settings.SmtpUser)
            };

            try
            {
                await client.SendMailAsync(msg);
                Console.WriteLine($"[Email] Sent to {to}: {subject}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email] SMTP error (host={settings.SmtpHost}:{settings.SmtpPort}, ssl={settings.SmtpEnableSsl}, user={settings.SmtpUser}): {ex.Message}");
            }
        });
    }

    /// <summary>发送邮箱验证邮件（后台）</summary>
    public void SendVerificationBackground(SiteSettings settings, string to, string token)
    {
        var link = $"{settings.SiteBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";
        var body = $"""
            <div style="max-width:480px;margin:0 auto;font-family:sans-serif">
              <h2>🫧 Aquarius 邮箱验证</h2>
              <p>感谢注册！请点击下方按钮激活账号：</p>
              <p style="text-align:center;margin:24px 0">
                <a href="{link}" style="display:inline-block;padding:12px 32px;background:#6366f1;color:#fff;border-radius:8px;text-decoration:none">激活账号</a>
              </p>
              <p style="color:#888;font-size:14px">或复制链接到浏览器：<br>{link}</p>
              <p style="color:#888;font-size:14px">链接 24 小时内有效。</p>
            </div>
            """;
        SendBackground(settings, to, "验证你的 Aquarius 邮箱", body);
    }

    /// <summary>发送邮箱变更确认邮件（后台）</summary>
    public void SendEmailChangeBackground(SiteSettings settings, string to, string token)
    {
        var link = $"{settings.SiteBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";
        var body = $"""
            <div style="max-width:480px;margin:0 auto;font-family:sans-serif">
              <h2>📧 Aquarius 邮箱变更确认</h2>
              <p>你正在修改账号绑定的邮箱地址，请点击下方按钮确认变更：</p>
              <p style="text-align:center;margin:24px 0">
                <a href="{link}" style="display:inline-block;padding:12px 32px;background:#f59e0b;color:#fff;border-radius:8px;text-decoration:none">确认变更</a>
              </p>
              <p style="color:#888;font-size:14px">或复制链接到浏览器：<br>{link}</p>
              <p style="color:#888;font-size:14px">链接 24 小时内有效。如果不是你本人操作，请忽略此邮件。</p>
            </div>
            """;
        SendBackground(settings, to, "确认你的 Aquarius 邮箱变更", body);
    }

    /// <summary>发送密码重置邮件（后台）</summary>
    public void SendPasswordResetBackground(SiteSettings settings, string to, string token)
    {
        var link = $"{settings.SiteBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";
        var body = $"""
            <div style="max-width:480px;margin:0 auto;font-family:sans-serif">
              <h2>🔑 Aquarius 密码重置</h2>
              <p>你请求了密码重置，请点击下方按钮设置新密码：</p>
              <p style="text-align:center;margin:24px 0">
                <a href="{link}" style="display:inline-block;padding:12px 32px;background:#f59e0b;color:#fff;border-radius:8px;text-decoration:none">重置密码</a>
              </p>
              <p style="color:#888;font-size:14px">或复制链接到浏览器：<br>{link}</p>
              <p style="color:#888;font-size:14px">链接 1 小时内有效。如果你没有请求重置密码，请忽略此邮件。</p>
            </div>
            """;
        SendBackground(settings, to, "重置你的 Aquarius 密码", body);
    }
}
