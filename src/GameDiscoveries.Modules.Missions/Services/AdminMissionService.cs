using GameDiscoveries.BuildingBlocks.Errors;
using GameDiscoveries.Modules.Missions.Data;
using GameDiscoveries.Modules.Missions.Domain;
using GameDiscoveries.Modules.Missions.Models;
using GameDiscoveries.Modules.Xp.Services;

namespace GameDiscoveries.Modules.Missions.Services;

public interface IAdminMissionService
{
    Task<IReadOnlyList<MissionTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default);

    Task<MissionTemplateDto> CreateAsync(Guid adminId, UpsertMissionTemplateRequest request, CancellationToken cancellationToken = default);

    Task<MissionTemplateDto> UpdateAsync(Guid adminId, Guid templateId, UpsertMissionTemplateRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(Guid adminId, Guid templateId, bool isActive, CancellationToken cancellationToken = default);

    Task<MissionAnalyticsResponse> GetAnalyticsAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminMissionService(
    IMissionStore store,
    IAuditLogService audit) : IAdminMissionService
{
    public async Task<IReadOnlyList<MissionTemplateDto>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await store.GetAllTemplatesAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<MissionTemplateDto> CreateAsync(
        Guid adminId,
        UpsertMissionTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var existing = await store.GetTemplateByCodeAsync(request.Code.Trim().ToUpperInvariant(), cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException("Mission code already exists.");
        }

        var entity = FromRequest(Guid.NewGuid(), request);
        await store.UpsertTemplateAsync(entity, cancellationToken);
        await audit.WriteAsync(
            adminId,
            "MISSION_CREATED",
            "MissionTemplate",
            entity.Id.ToString("D"),
            null,
            ToDto(entity),
            null,
            cancellationToken: cancellationToken);
        return ToDto(entity);
    }

    public async Task<MissionTemplateDto> UpdateAsync(
        Guid adminId,
        Guid templateId,
        UpsertMissionTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var before = await store.GetTemplateByIdAsync(templateId, cancellationToken)
            ?? throw new NotFoundException("Mission", "Mission template not found.");

        // Keep code immutable for historical integrity
        var entity = FromRequest(templateId, request with { Code = before.Code });
        await store.UpsertTemplateAsync(entity, cancellationToken);
        await audit.WriteAsync(
            adminId,
            "MISSION_UPDATED",
            "MissionTemplate",
            templateId.ToString("D"),
            ToDto(before),
            ToDto(entity),
            null,
            cancellationToken: cancellationToken);
        return ToDto(entity);
    }

    public async Task SetActiveAsync(
        Guid adminId,
        Guid templateId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var before = await store.GetTemplateByIdAsync(templateId, cancellationToken)
            ?? throw new NotFoundException("Mission", "Mission template not found.");

        await store.SetTemplateActiveAsync(templateId, isActive, cancellationToken);
        await audit.WriteAsync(
            adminId,
            isActive ? "MISSION_ACTIVATED" : "MISSION_DEACTIVATED",
            "MissionTemplate",
            templateId.ToString("D"),
            new { before.IsActive },
            new { isActive },
            null,
            cancellationToken: cancellationToken);
    }

    public Task<MissionAnalyticsResponse> GetAnalyticsAsync(CancellationToken cancellationToken = default) =>
        store.GetAnalyticsAsync(cancellationToken);

    private static void Validate(UpsertMissionTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ValidationException("Code is required.");
        }

        if (request.Type is not (MissionTypes.Daily or MissionTypes.Weekly))
        {
            throw new ValidationException("Type must be DAILY or WEEKLY.");
        }

        if (!MissionRequirementTypes.All.Contains(request.RequirementType))
        {
            throw new ValidationException("Invalid requirement type.");
        }

        if (request.TargetValue <= 0)
        {
            throw new ValidationException("TargetValue must be > 0.");
        }

        if (request.RewardXp < 0)
        {
            throw new ValidationException("RewardXp must be >= 0.");
        }

        if (request.Difficulty is not (MissionDifficulties.Easy or MissionDifficulties.Medium or MissionDifficulties.Hard))
        {
            throw new ValidationException("Difficulty must be EASY, MEDIUM, or HARD.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("Title is required.");
        }
    }

    private static MissionTemplateEntity FromRequest(Guid id, UpsertMissionTemplateRequest request) =>
        new()
        {
            Id = id,
            Code = request.Code.Trim().ToUpperInvariant(),
            Type = request.Type.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Icon = request.Icon,
            RequirementType = request.RequirementType.Trim().ToUpperInvariant(),
            TargetValue = request.TargetValue,
            RewardXp = request.RewardXp,
            Difficulty = request.Difficulty.Trim().ToUpperInvariant(),
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

    private static MissionTemplateDto ToDto(MissionTemplateEntity e) =>
        new(e.Id, e.Code, e.Type, e.Title, e.Description, e.Icon, e.RequirementType,
            e.TargetValue, e.RewardXp, e.Difficulty, e.IsActive, e.SortOrder, e.UpdatedAt);
}
