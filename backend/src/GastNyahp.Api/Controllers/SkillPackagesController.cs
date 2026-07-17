using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/skill-packages")]
public sealed partial class SkillPackagesController : ControllerBase
{
    const string PackageName = "gastnyahp";
    const string PackageVersion = "1.0.0";
    static readonly string[] SkillNames =
        ["gastnyahp", "gastnyahp-consulta", "gastnyahp-abm", "gastnyahp-instrumentos"];

    static string PackageRoot => Path.Combine(AppContext.BaseDirectory, "SkillPackages", PackageName);

    [HttpGet(PackageName)]
    public IActionResult Manifest()
    {
        var skills = SkillNames.Select(name =>
        {
            var bytes = System.IO.File.ReadAllBytes(SkillPath(name));
            var text = Encoding.UTF8.GetString(bytes);
            return new
            {
                name,
                description = FrontmatterValue(text, "description"),
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                path = $"{name}/SKILL.md",
                url = $"/api/skill-packages/{PackageName}/skills/{name}"
            };
        });

        return Ok(new
        {
            name = PackageName,
            version = PackageVersion,
            format = "skill-package-v1",
            description = "Guías conversacionales para consultar y operar GastNyahp mediante sus tools MCP.",
            mcpPath = "/mcp",
            downloadUrl = $"/api/skill-packages/{PackageName}/download",
            skills
        });
    }

    [HttpGet(PackageName + "/skills/{skillName}")]
    public IActionResult Skill(string skillName)
    {
        if (!SkillNames.Contains(skillName, StringComparer.Ordinal)) return NotFound();
        return PhysicalFile(SkillPath(skillName), "text/markdown; charset=utf-8", enableRangeProcessing: false);
    }

    [HttpGet(PackageName + "/download")]
    public IActionResult Download()
    {
        using var archiveBytes = new MemoryStream();
        using (var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in SkillNames)
            {
                var entry = archive.CreateEntry($"{name}/SKILL.md", CompressionLevel.Optimal);
                using var output = entry.Open();
                using var input = System.IO.File.OpenRead(SkillPath(name));
                input.CopyTo(output);
            }

            var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(manifest.Open(), new UTF8Encoding(false));
            writer.Write($$"""{"name":"{{PackageName}}","version":"{{PackageVersion}}","format":"skill-package-v1","skills":["{{string.Join("\",\"", SkillNames)}}"]}""");
        }

        var bytes = archiveBytes.ToArray();
        var etag = '"' + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + '"';
        if (Request.Headers.IfNoneMatch.Contains(etag))
            return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public,max-age=3600";
        return File(bytes, "application/zip", $"gastnyahp-skills-{PackageVersion}.zip");
    }

    static string SkillPath(string name)
    {
        var path = Path.Combine(PackageRoot, name, "SKILL.md");
        if (!System.IO.File.Exists(path))
            throw new FileNotFoundException($"La skill empaquetada '{name}' no fue publicada.", path);
        return path;
    }

    static string FrontmatterValue(string markdown, string key)
    {
        var match = FrontmatterLine().Match(markdown);
        return match.Success && match.Groups["key"].Value == key
            ? match.Groups["value"].Value.Trim()
            : markdown.Split('\n')
                .Select(line => FrontmatterLine().Match(line))
                .Where(m => m.Success && m.Groups["key"].Value == key)
                .Select(m => m.Groups["value"].Value.Trim())
                .FirstOrDefault() ?? "";
    }

    [GeneratedRegex(@"^(?<key>[a-zA-Z0-9_-]+):\s*(?<value>.+)\r?$", RegexOptions.Multiline)]
    private static partial Regex FrontmatterLine();
}