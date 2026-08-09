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

    /* ─── GetCollectorProgressQuery ───────────────────────── */

    [Test]
    public async Task GetCollectorProgress_CalculatesPercent()
    {
        var db = new Mock<IDbCardStore>();
        db.Setup(s => s.GetCollectorProgressRawAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CollectorProgressRaw> { new("m19", null, 269, 135, 80) });

        var result = await new GetCollectorProgressQueryHandler(db.Object).Handle(new GetCollectorProgressQuery());
        Assert.That(result.First().Percent, Is.GreaterThan(50));
    }

    [Test]
    public async Task GetCollectorProgress_OnlyOwn_True_ShouldFilterToOwnedCardsOnly()
    {
        // BUG: Handler ignores OnlyOwn parameter — Percent is calculated from all collected cards
        // instead of filtering to owned-only. The IDbCardStore.GetCollectorProgressRawAsync
        // does not accept an onlyOwn parameter, and the handler performs no client-side filtering.
        // Fix: Either add GetCollectorProgressRawAsync(bool onlyOwn) overload, or filter
        // Collected/Percent in the handler when OnlyOwn=true.

        var db = new Mock<IDbCardStore>();

        // Simulate: 269 total cards in KB, 135 collected (any), 80 actually owned
        var allCards = new List<CollectorProgressRaw> { new("m19", null, 269, 135, 80) };

        db.Setup(s => s.GetCollectorProgressRawAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allCards);

        var handler = new GetCollectorProgressQueryHandler(db.Object);

        var allResult = await handler.Handle(new GetCollectorProgressQuery(OnlyOwn: false));
        var ownedResult = await handler.Handle(new GetCollectorProgressQuery(OnlyOwn: true));

        // OnlyOwn=true should produce a lower Percent (80/269=29.7%) than OnlyOwn=false (135/269=50.2%)
        // Currently both return the same value because OnlyOwn is ignored — this will fail.
        Assert.That(
            ownedResult.First().Percent,
            Is.LessThan(allResult.First().Percent),
            "BUG: OnlyOwn=true should reduce Percent to reflect only owned cards. " +
            $"Expected < {allResult.First().Percent:F2}% (all collected), " +
            $"got {ownedResult.First().Percent:F2}%. Handler ignores OnlyOwn parameter entirely.");
    }

    [Test]
    public async Task GetCollectorProgress_Sorting_ShouldPutCompleteSetsFirst()
    {
        // BUG: Handler sorts OrderBy(r => r.Percent) (ascending), but the comment says
        // "Sort by progress (complete first)". Complete sets have 100% — ascending puts them last.
        // Fix: Change to OrderDescending or reverse the sort logic.

        var db = new Mock<IDbCardStore>();
        db.Setup(s => s.GetCollectorProgressRawAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CollectorProgressRaw>
            {
                new("m19", "Magic 2019", 269, 269, 200), // 100% — complete
                new("m20", "Magic 2020", 273, 50, 40),   // ~18% — incomplete
            });

        var result = await new GetCollectorProgressQueryHandler(db.Object)
            .Handle(new GetCollectorProgressQuery());

        var first = result.First();
        var last = result.Last();

        Assert.That(
            first.Percent >= last.Percent,
            Is.True,
            $"BUG: Sorting is ascending but comment says 'complete first'. " +
            $"First set '{first.SetAbbreviation}' has {first.Percent:F1}%, " +
            $"last set '{last.SetAbbreviation}' has {last.Percent:F1}%. " +
            $"Complete sets (100%) should appear before incomplete ones.");
    }
}
