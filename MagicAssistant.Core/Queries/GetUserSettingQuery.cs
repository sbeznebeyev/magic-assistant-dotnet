using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Queries;

/// <summary>
/// Query to retrieve a user setting value by key.
/// </summary>
public record GetUserSettingQuery(string Key);

public class GetUserSettingQueryHandler
{
    private readonly IUserSettingsStore _settingsStore;

    public GetUserSettingQueryHandler(IUserSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<string?> Handle(GetUserSettingQuery query, CancellationToken ct = default)
    {
        return await _settingsStore.GetValueAsync(query.Key, ct);
    }
}
