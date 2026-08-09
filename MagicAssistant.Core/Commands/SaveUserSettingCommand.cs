using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Command to save or update a user setting value in the database.
/// </summary>
public record SaveUserSettingCommand(string Key, string Value);

public class SaveUserSettingCommandHandler
{
    private readonly IUserSettingsStore _settingsStore;

    public SaveUserSettingCommandHandler(IUserSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public async Task<bool> Handle(SaveUserSettingCommand command, CancellationToken ct = default)
    {
        return await _settingsStore.SetValueAsync(command.Key, command.Value, ct);
    }
}
