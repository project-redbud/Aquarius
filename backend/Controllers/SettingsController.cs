using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;

namespace Aquarius.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AquariusDbContext _db;

    public SettingsController(AquariusDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var s = await _db.SiteSettings.FirstOrDefaultAsync();
        if (s == null) return Ok(new { siteName = "Aquarius", copyright = "", siteBaseUrl = "" });
        return Ok(new { siteName = s.SiteName, copyright = s.Copyright, siteBaseUrl = s.SiteBaseUrl });
    }
}
