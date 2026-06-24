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
            Credentials = string.IsNullOrWhiteSpace(s.SmtpUser)
                ? null
                : new NetworkCredential(s.SmtpUser, s.SmtpPassword)
        };
        return client;
    }

    /// <summary>发送邮件。如 SMTP 未配置则静默跳过（开发环境）。</summary>
    public async Task SendAsync(SiteSettings settings, string to, string subject, string body)
    {
        using var client = BuildClient(settings);
        if (client == null)
        {
            // SMTP 未配置：记录日志但不报错（开发环境可接受）
            Console.WriteLine($"[Email] SMTP not configured. Would send to {to}: {subject}");
            return;
        }

        var from = string.IsNullOrWhiteSpace(settings.SmtpFrom) ? settings.SmtpUser : settings.SmtpFrom;
        using var msg = new MailMessage(from, to, subject, body)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(msg);
    }

    /// <summary>发送邮箱验证邮件</summary>
    public async Task SendVerificationAsync(SiteSettings settings, string to, string token)
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
        await SendAsync(settings, to, "验证你的 Aquarius 邮箱", body);
    }

    /// <summary>发送密码重置邮件</summary>
    public async Task SendPasswordResetAsync(SiteSettings settings, string to, string token)
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
        await SendAsync(settings, to, "重置你的 Aquarius 密码", body);
    }
}
