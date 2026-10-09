using MagicAssistant.App.Services;
using MagicAssistant.Core;
using MagicAssistant.Core.Analysis;
using MagicAssistant.Core.Commands;
using MagicAssistant.Core.Queries;

using MagicAssistant.Core.Services.CardResolve;
using MagicAssistant.Core.Services.CardSearch;
using MagicAssistant.Core.Services.DeckAnalysis;
using MagicAssistant.Core.Services.DeckCard;
using MagicAssistant.Core.Services.DeckImportExport;
using MagicAssistant.Core.Services.DeckManagement;
using MagicAssistant.Core.Services.KnowledgeBaseSync;

using MagicAssistant.Data.Caches;
using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Db;
using MagicAssistant.Data.Model;
using MagicAssistant.Http.Contracts;
using MagicAssistant.Http.Scryfall;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(); // ImagesController
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Circuit timeout: 5 minutes of inactivity
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);
        // JS interop timeout: 60 seconds
        options.JSInteropDefaultCallTimeout = TimeSpan.FromSeconds(60);
    });

// SignalR configuration for Blazor Server circuit
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 64 * 1024; // 64 KB
});

// Register Memory Cache
builder.Services.AddMemoryCache();

// Chart.js for analysis visualizations
// Chart.js removed — using pure CSS visualizations

// Configuration — data path from appsettings.json / environment variable / CLI
// If relative, resolved against AppContext.BaseDirectory; if absolute, used as-is.
var configuredDataPath = builder.Configuration["DataPath"];
var dataPath = string.IsNullOrEmpty(configuredDataPath)
    ? Path.Combine(AppContext.BaseDirectory, "data")
    : Path.IsPathRooted(configuredDataPath)
        ? Path.GetFullPath(configuredDataPath)
        : Path.Combine(AppContext.BaseDirectory, configuredDataPath);

// Ensure the base data directory exists
if (!Directory.Exists(dataPath))
    Directory.CreateDirectory(dataPath);

var dbPath = Path.Combine(dataPath, "db");
var imagesDir = Path.Combine(dataPath, "images");

// Ensure subdirectories exist
Directory.CreateDirectory(dbPath);
Directory.CreateDirectory(imagesDir);

var cardsDb = Path.Combine(dbPath, "cards.db");

// HTTP clients
builder.Services.AddHttpClient<IScrayfallClient, ScryfallClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddHttpClient("image-cache", client =>
{
    client.BaseAddress = new Uri("https://cards.scryfall.io");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MagicAssistant/1.0");
});

// Image cache service (singleton — shared state across requests)
builder.Services.AddSingleton<IImageCacheService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("image-cache");
    return new ImageCacheService(imagesDir, httpClient);
});

// Set icons cache service (singleton — shared state across requests)
var setIconsDir = Path.Combine(dataPath, "set_icons");
Directory.CreateDirectory(setIconsDir);
builder.Services.AddSingleton<ISetIconsCacheService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("image-cache");
    return new SetIconsCacheService(setIconsDir, httpClient);
});

// Sync state registry (singleton — tracks all active syncs)
builder.Services.AddSingleton<SyncSetStateRegistry>();

// Data layer — KnowledgeBase (SQLite)
builder.Services.AddSingleton<IDbCardStore>(_ => new SqliteCardStore(cardsDb));

// Data layer — KnowledgeBase (SQLite)
builder.Services.AddSingleton<CardSetsCache>();
builder.Services.AddSingleton<AllCardSetsCache>();

// Data layer — User Settings Store (SQLite, same DB as KnowledgeBase)
builder.Services.AddSingleton<IUserSettingsStore>(_ => new SqliteUserSettingsStore(cardsDb));

// Data layer — Collection Store (SQLite, same DB as KnowledgeBase)
builder.Services.AddSingleton<ICollectionStore>(_ => new SqliteCollectionStore(cardsDb));
builder.Services.AddSingleton<ICardStore<MyCard>>(sp =>
    sp.GetRequiredService<ICollectionStore>() as ICardStore<MyCard>
    ?? throw new InvalidOperationException("ICollectionStore does not implement ICardStore<MagicCardPhysical>"));

// Core commands & queries — scoped per Blazor circuit (one instance per user session)
// Scoped prevents race conditions when multiple requests hit the same handler.
// Handlers are stateless delegates; their dependencies (stores, clients) remain Singleton.
builder.Services.AddScoped<AddCardsToDeckCommandHandler>();
builder.Services.AddScoped<GetDeckCardsQueryHandler>();
builder.Services.AddScoped<SearchCardsQueryHandler>();
builder.Services.AddScoped<SyncKBCommandHandler>(); // delegates to KnowledgeBaseSyncService
builder.Services.AddScoped<GetScryfallAvailableSetsQueryHandler>();
builder.Services.AddScoped<GetSyncProgressQueryHandler>();
builder.Services.AddScoped<GetSetSyncStatusQueryHandler>();
builder.Services.AddScoped<GetSetSyncStatusByCodesQueryHandler>();
builder.Services.AddScoped<ResolveLegacyCardsQueryHandler>();
builder.Services.AddScoped<ValidateCardIdsQueryHandler>();
builder.Services.AddScoped<ImportResolvedCardsCommandHandler>();
builder.Services.AddScoped<RemoveCardsFromDeckCommandHandler>();
builder.Services.AddScoped<MoveCardsCommandHandler>();
builder.Services.AddScoped<UpdateCardCommandHandler>();
builder.Services.AddScoped<CreateNodeCommandHandler>();
builder.Services.AddScoped<DeleteNodeCommandHandler>();
builder.Services.AddScoped<RenameNodeCommandHandler>();
builder.Services.AddScoped<LockNodeCommandHandler>();
builder.Services.AddScoped<UnlockNodeCommandHandler>();
builder.Services.AddScoped<GetDeckAnalysisQueryHandler>();
builder.Services.AddScoped<GetNodesTreeQueryHandler>();
builder.Services.AddScoped<GetNodeByIdQueryHandler>();
builder.Services.AddScoped<GetNodesBranchQueryHandler>();
builder.Services.AddScoped<GetCardByIdQueryHandler>();
builder.Services.AddScoped<GetCardPrintingsQueryHandler>();
builder.Services.AddScoped<GetCardLanguagesQueryHandler>();
builder.Services.AddScoped<GetCardInstancesQueryHandler>();
builder.Services.AddScoped<GetKBAvailableSetsQueryHandler>();
builder.Services.AddScoped<GetCardSetQueryHandler>();
builder.Services.AddScoped<HasCardsQueryHandler>();
builder.Services.AddScoped<SyncCardSetsCommandHandler>();
// On-demand per-card image download (lazy loading) — reuses the set-sync image pipeline
builder.Services.AddScoped<DownloadCardImagesCommandHandler>();
// User Settings commands & queries
builder.Services.AddScoped<SaveUserSettingCommandHandler>();
builder.Services.AddScoped<GetUserSettingQueryHandler>();

/// KB refresh notification service (scoped — one per circuit)
builder.Services.AddScoped<KbRefreshService>();

// Vertical slice services
builder.Services.AddScoped<IDeckAnalysisService, DeckAnalysisService>();
builder.Services.AddScoped<IDeckCardService, DeckCardService>();
builder.Services.AddScoped<IDeckImportExportService, DeckImportExportService>(sp =>
    new DeckImportExportService(
        sp.GetRequiredService<ICollectionStore>(),
        sp.GetRequiredService<ICardSearchService>(),
        sp.GetRequiredService<GetSetSyncStatusQueryHandler>(),
        sp.GetRequiredService<GetSetSyncStatusByCodesQueryHandler>(),
        sp.GetRequiredService<ResolveLegacyCardsQueryHandler>(),
        sp.GetRequiredService<ValidateCardIdsQueryHandler>(),
        sp.GetRequiredService<SyncKBCommandHandler>(),
        sp.GetRequiredService<ImportResolvedCardsCommandHandler>()));
builder.Services.AddScoped<IDeckManagementService, DeckManagementService>();
// Lazy card image loading for the UI (checks local cache, triggers per-card download)
builder.Services.AddScoped<ICardImageLoader, CardImageLoader>();
builder.Services.AddScoped<ICardResolveService, CardResolveService>();
builder.Services.AddScoped<ICardSearchService, CardSearchService>();
builder.Services.AddScoped<IKnowledgeBaseSyncService, KnowledgeBaseSyncService>();


// Analysis service — stateless, registered as Singleton (shared across all circuits)
builder.Services.AddSingleton<IAnalysisService, AnalysisService>();

// SqliteGuidHandler registers itself via static constructor — no manual registration needed.

var app = builder.Build();

// Initialize DB — create all tables and indexes (idempotent)
await InitializeDb.EnsureCreatedAsync(cardsDb);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Static files MUST come before routing for Blazor framework scripts
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery(); // Required for MapRazorComponents

// ImagesController — serve cached card images (GET /api/images/{fileName})
app.MapControllers();

// Blazor Web App — modern .NET 10 approach with MapRazorComponents
app.MapRazorComponents<MagicAssistant.App.App>()
    .AddInteractiveServerRenderMode();

app.Run();