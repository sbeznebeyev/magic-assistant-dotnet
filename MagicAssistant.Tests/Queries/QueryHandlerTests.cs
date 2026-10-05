using MagicAssistant.Core;
using MagicAssistant.Core.Queries;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;
using MagicAssistant.Http.Contracts;
using MagicAssistant.Http.Dtos;

namespace MagicAssistant.Tests.Queries;

[TestFixture]
public class QueryHandlerTests
{
    private static ScryfallSetDto SetDto(string code, string name)
        => new(
            Id: Guid.NewGuid(),
            Code: code,
            Name: name,
            Uri: $"https://api.scryfall.com/sets/{code}",
            ScryfallUri: $"https://scryfall.com/sets/{code}",
            SearchUri: $"https://api.scryfall.com/cards/search?q=e%3A{code}",
            ReleasedAt: "2020-01-01",
            SetType: "core",
            CardCount: 269,
            ParentSetCode: null,
            Digital: false,
            NonfoilOnly: false,
            FoilOnly: false,
            IconSvgUri: $"https://svgs.scryfall.io/sets/{code}.svg");

    /* ─── GetAvailableSetsQuery (caching) ──────────────────── */

    [Test]
    public async Task GetAvailableSets_FirstCall_FetchesFromApi()
    {
        var scryfall = new Mock<IScrayfallClient>();
        scryfall.Setup(s => s.GetSetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { SetDto("m19", "Magic 2019"), SetDto("m20", "Magic 2020") });

        var handler = new GetScryfallAvailableSetsQueryHandler(scryfall.Object);
        var result = await handler.Handle(new GetScryfallAvailableSetsQuery());

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAvailableSets_SecondCall_UsesCache()
    {
        var scryfall = new Mock<IScrayfallClient>();
        scryfall.Setup(s => s.GetSetsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { SetDto("m19", "Magic 2019") });

        var handler = new GetScryfallAvailableSetsQueryHandler(scryfall.Object);
        await handler.Handle(new GetScryfallAvailableSetsQuery());
        await handler.Handle(new GetScryfallAvailableSetsQuery());

        scryfall.Verify(s => s.GetSetsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /* ─── GetSyncProgressQuery (sync handler) ─────────────── */

    [Test]
    public void GetSyncProgress_StateExists_ReturnsFullProgress()
    {
        var registry = new SyncSetStateRegistry();
        var state = new SyncSetState("m19")
        {
            Phase = 3, PhaseName = "Images", TotalCards = 269, ReadyCardsToSave = 150,
            ImagesDownloaded = 200, ImagesFailed = 5, Message = "Downloading...", SetName = "Magic 2019"
        };
        registry.Register(state);

        var handler = new GetSyncProgressQueryHandler(registry);
        var result = handler.Handle(new GetSyncProgressQuery("m19"));

        Assert.That(result!.SetAbbreviation, Is.EqualTo("m19"));
        Assert.That(result.Phase, Is.EqualTo(3));
        Assert.That(result.ImagesDownloaded, Is.EqualTo(200));
    }

    [Test]
    public void GetSyncProgress_CompletedState_ReturnsElapsed()
    {
        var registry = new SyncSetStateRegistry();
        registry.Register(new SyncSetState("m19") { Completed = true });

        var result = new GetSyncProgressQueryHandler(registry).Handle(new GetSyncProgressQuery("m19"));
        Assert.That(result!.Completed, Is.True);
        Assert.That(result.Elapsed, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public void GetSyncProgress_FailedState_ReturnsErrorMessage()
    {
        var registry = new SyncSetStateRegistry();
        registry.Register(new SyncSetState("m19") { Failed = true, ErrorMessage = "API timeout" });

        var result = new GetSyncProgressQueryHandler(registry).Handle(new GetSyncProgressQuery("m19"));
        Assert.That(result!.Failed, Is.True);
        Assert.That(result.ErrorMessage, Is.EqualTo("API timeout"));
    }

    [Test]
    public void GetSyncProgress_StateNotFound_ReturnsNull()
    {
        var result = new GetSyncProgressQueryHandler(new SyncSetStateRegistry())
            .Handle(new GetSyncProgressQuery("NONEXIST"));
        Assert.That(result, Is.Null);
    }

    /* ─── GetDeckAnalysisQuery ────────────────────────────── */

    [Test]
    public async Task GetDeckAnalysis_UsesSideboard()
    {
        var coll = new Mock<ICollectionStore>();
        var analysis = new Mock<IAnalysisService>();
        coll.Setup(s => s.GetDeckWithSideboardAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MagicCardPhysical>());
        analysis.Setup(a => a.Analyze(It.IsAny<IEnumerable<MagicCardPhysical>>()))
            .Returns(new DeckAnalysisResult());

        var handler = new GetDeckAnalysisQueryHandler(coll.Object, analysis.Object);
        await handler.Handle(new GetDeckAnalysisQuery(1));

        coll.Verify(s => s.GetDeckWithSideboardAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }
}
