namespace MagicAssistant.Core.Queries;

/// <summary>
/// Query to poll sync progress for a specific set.
/// Used by the frontend polling endpoint.
/// </summary>
public record GetSyncProgressQuery(string SetAbbreviation);

/// <summary>
/// Response DTO for sync progress polling.
/// </summary>
public record SyncProgressResponse(
    string SetAbbreviation,
    string? SetName,
    int TotalCards,
    int ReadyCardsToSave,
    double ProgressPercent,
    int Phase,
    string PhaseName,
    int ImagesDownloaded,
    int ImagesFailed,
    string Message,
    bool Completed,
    bool Failed,
    string? ErrorMessage,
    TimeSpan Elapsed
);

public class GetSyncProgressQueryHandler
{
    private readonly SyncSetStateRegistry _registry;

    public GetSyncProgressQueryHandler(SyncSetStateRegistry registry)
    {
        _registry = registry;
    }

    public SyncProgressResponse? Handle(GetSyncProgressQuery query)
    {
        var state = _registry.Get(query.SetAbbreviation);
        if (state == null)
            return null;

        return new SyncProgressResponse(
            SetAbbreviation: state.SetCode,
            SetName: state.SetName,
            TotalCards: state.TotalCards,
            ReadyCardsToSave: state.ReadyCardsToSave,
            ProgressPercent: Math.Round(state.ProgressPercent),
            Phase: state.Phase,
            PhaseName: state.PhaseName,
            ImagesDownloaded: state.ImagesDownloaded,
            ImagesFailed: state.ImagesFailed,
            Message: state.Message,
            Completed: state.Completed,
            Failed: state.Failed,
            ErrorMessage: state.ErrorMessage,
            Elapsed: state.Elapsed
        );
    }
}
