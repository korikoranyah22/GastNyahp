using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GastNyahp.E2E.Tests.Support;

namespace GastNyahp.E2E.Tests;

public sealed class SkillPackageTests : IClassFixture<GastNyahpApiFactory>
{
    readonly HttpClient _client;

    public SkillPackageTests(GastNyahpApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Manifest_is_public_and_separates_the_four_operational_guides()
    {
        var response = await _client.GetAsync("/api/skill-packages/gastnyahp");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var manifest = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("skill-package-v1", manifest.GetProperty("format").GetString());
        Assert.Equal("/api/skill-packages/gastnyahp/download", manifest.GetProperty("downloadUrl").GetString());

        var names = manifest.GetProperty("skills").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()!)
            .ToArray();
        Assert.Equal(
            ["gastnyahp", "gastnyahp-consulta", "gastnyahp-abm", "gastnyahp-instrumentos"],
            names);
    }

    [Fact]
    public async Task Download_is_a_zip_with_manifest_and_one_skill_file_per_guide()
    {
        var response = await _client.GetAsync("/api/skill-packages/gastnyahp/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Headers.ETag);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("manifest.json"));
        Assert.NotNull(zip.GetEntry("gastnyahp/SKILL.md"));
        Assert.NotNull(zip.GetEntry("gastnyahp-consulta/SKILL.md"));
        Assert.NotNull(zip.GetEntry("gastnyahp-abm/SKILL.md"));
        Assert.NotNull(zip.GetEntry("gastnyahp-instrumentos/SKILL.md"));
        Assert.Equal(5, zip.Entries.Count);
    }

    [Fact]
    public async Task Individual_skill_endpoint_only_serves_allowlisted_names()
    {
        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync("/api/skill-packages/gastnyahp/skills/gastnyahp-consulta")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync("/api/skill-packages/gastnyahp/skills/..%2F..%2Fappsettings.json")).StatusCode);
    }
}