using MagicAssistant.Data.Contracts;

namespace MagicAssistant.Data.IO;

/// <summary>
/// Parsed deck/collection file — header fields plus the card list.
/// Shared result type of <c>XmlImport</c> (legacy format) and <c>JsonImport</c>
/// (native format), so downstream services stay format-agnostic.
/// </summary>
public class CardCollectionImportResult
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Legacy XML only — the native JSON format has no key field.</summary>
    public string? Key { get; set; }

    public string? Comment { get; set; }

    public string? Type { get; set; }

    public List<IMagicCard> Cards { get; set; } = new();
}
