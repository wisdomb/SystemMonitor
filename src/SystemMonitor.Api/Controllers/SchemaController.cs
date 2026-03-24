using Microsoft.AspNetCore.Mvc;
using SystemMonitor.Api.Services;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Controllers;

[ApiController]
[Route("api/v1/schema")]
public class SchemaController : ControllerBase
{
    private readonly ISchemaRegistryRepository _registry;
    private readonly IAttributeNormalizationService _normalizer;
    private readonly ILogger<SchemaController> _logger;

    public SchemaController(
        ISchemaRegistryRepository registry,
        IAttributeNormalizationService normalizer,
        ILogger<SchemaController> logger)
    {
        _registry = registry;
        _normalizer = normalizer;
        _logger = logger;
    }

    [HttpGet("canonical")]
    public async Task<IActionResult> GetAllCanonical(CancellationToken ct)
    {
        var attrs = await _registry.GetAllCanonicalAttributesAsync(ct);
        return Ok(attrs.OrderBy(a => a.Category).ThenBy(a => a.Name));
    }

    [HttpGet("canonical/{id}")]
    public async Task<IActionResult> GetCanonical(string id, CancellationToken ct)
    {
        var attr = await _registry.GetCanonicalAttributeAsync(id, ct);
        return attr is null ? NotFound() : Ok(attr);
    }

    [HttpPost("canonical")]
    public async Task<IActionResult> CreateCanonical(
        [FromBody] CanonicalAttribute attribute, CancellationToken ct)
    {
        var attr = attribute with
        {
            Id = string.IsNullOrEmpty(attribute.Id)
                        ? attribute.Name.ToLowerInvariant().Replace(" ", "_")
                        : attribute.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _registry.UpsertCanonicalAttributeAsync(attr, ct);
        return Created($"/api/v1/schema/canonical/{attr.Id}", attr);
    }

    [HttpPut("canonical/{id}")]
    public async Task<IActionResult> UpdateCanonical(
        string id, [FromBody] CanonicalAttribute attribute, CancellationToken ct)
    {
        var existing = await _registry.GetCanonicalAttributeAsync(id, ct);
        if (existing is null) return NotFound();

        await _registry.UpdateCanonicalAttributeAsync(
            attribute with { Id = id, UpdatedAt = DateTimeOffset.UtcNow }, ct);
        return Ok();
    }

    [HttpDelete("canonical/{id}")]
    public async Task<IActionResult> DeleteCanonical(string id, CancellationToken ct)
    {
        await _registry.DeleteCanonicalAttributeAsync(id, ct);
        return NoContent();
    }

    [HttpPost("canonical/{id}/aliases")]
    public async Task<IActionResult> AddAlias(
        string id, [FromBody] AddAliasRequest request, CancellationToken ct)
    {
        var attr = await _registry.GetCanonicalAttributeAsync(id, ct);
        if (attr is null) return NotFound();

        if (attr.KnownAliases.Contains(request.Alias, StringComparer.OrdinalIgnoreCase))
            return Conflict(new { message = "Alias already exists" });

        var updated = attr with
        {
            KnownAliases = attr.KnownAliases.Append(request.Alias).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _registry.UpdateCanonicalAttributeAsync(updated, ct);
        return Ok(updated);
    }

    [HttpGet("unknown")]
    public async Task<IActionResult> GetPendingUnknowns(CancellationToken ct)
    {
        var unknowns = await _registry.GetPendingUnknownAttributesAsync(ct);
        return Ok(unknowns);
    }

    [HttpGet("unknown/stats")]
    public async Task<IActionResult> GetUnknownStats(CancellationToken ct)
    {
        var unknowns = await _registry.GetPendingUnknownAttributesAsync(ct);
        return Ok(new
        {
            pending = unknowns.Count,
            withSuggestion = unknowns.Count(u => u.SuggestedCanonicalId is not null),
            highConfidence = unknowns.Count(u => u.SuggestionConfidence >= 0.70),
            totalOccurrences = unknowns.Sum(u => u.OccurrenceCount)
        });
    }

    [HttpPost("unknown/{id}/confirm")]
    public async Task<IActionResult> ConfirmMapping(
        string id, [FromBody] ConfirmMappingRequest request, CancellationToken ct)
    {
        await _normalizer.ConfirmMappingAsync(
            id, request.CanonicalId, request.ReviewedBy, request.Note, ct);
        return Ok(new { message = "Mapping confirmed and alias registered" });
    }

    [HttpPost("unknown/{id}/reject")]
    public async Task<IActionResult> RejectMapping(
        string id, [FromBody] ReviewActionRequest request, CancellationToken ct)
    {
        await _normalizer.RejectMappingAsync(id, request.ReviewedBy, request.Note, ct);
        return Ok();
    }

    [HttpPost("resolve/test")]
    public async Task<IActionResult> TestResolve(
        [FromBody] TestResolveRequest request, CancellationToken ct)
    {
        var result = await _normalizer.ResolveAsync(
            request.RawName, "test", "test", ct);
        return Ok(result);
    }

    [HttpPost("resolve/bulk-test")]
    public async Task<IActionResult> BulkTestResolve(
        [FromBody] BulkTestResolveRequest request, CancellationToken ct)
    {
        var tasks = request.RawNames
            .Select(n => _normalizer.ResolveAsync(n, "bulk-test", "test", ct));
        var results = await Task.WhenAll(tasks);

        return Ok(new
        {
            total = results.Length,
            resolved = results.Count(r => r.IsResolved),
            unresolved = results.Count(r => !r.IsResolved),
            rate = (double)results.Count(r => r.IsResolved) / results.Length,
            results = results
        });
    }
}

public record AddAliasRequest(string Alias);
public record ConfirmMappingRequest(string CanonicalId, string ReviewedBy, string? Note);
public record ReviewActionRequest(string ReviewedBy, string? Note);
public record TestResolveRequest(string RawName);
public record BulkTestResolveRequest(List<string> RawNames);
