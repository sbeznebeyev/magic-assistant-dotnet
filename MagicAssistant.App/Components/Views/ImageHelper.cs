using MagicAssistant.Data.Model;

namespace MagicAssistant.App.Components.Views;

public static class ImageHelper
{
    public static string? GetImageUrl(MagicCard? card, bool getFrontImage)
    {
        if (card == null) return null;
        var (setFolder, lang, fileName) = (card.SetCode!, card.Language!, card.Id.ToString());
        if (!string.IsNullOrEmpty(fileName))
        {
            return getFrontImage ? $"/api/images/{setFolder}/{lang}/front/{fileName}" : $"/api/images/{setFolder}/{lang}/back/{fileName}";
        }
        return null;
    }
}
