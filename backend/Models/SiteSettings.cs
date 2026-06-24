namespace Aquarius.Api.Models;

public class SiteSettings
{
    public int Id { get; set; } = 1;
    public string SiteName { get; set; } = "Aquarius";
    public string Copyright { get; set; } = "";

    // ── SMTP ────────────────────────────────────────────────
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string SmtpHost { get; set; } = "";

    public int SmtpPort { get; set; } = 587;

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string SmtpUser { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string SmtpPassword { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string SmtpFrom { get; set; } = "";

    public bool SmtpEnableSsl { get; set; } = true;

    /// <summary>站点基础 URL，用于生成邮件中的链接（如 https://example.com）</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string SiteBaseUrl { get; set; } = "";
}
