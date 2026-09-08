using MagicAssistant.Data.Contracts;
using MagicAssistant.Data.Model;

namespace MagicAssistant.Core.Commands;

/// <summary>
/// Update properties of a card instance in a deck/collection.
/// Mirrors Java: DataManager.updateMCP() + EditMagicCardPhysicalDialog
/// </summary>
public record UpdateCardCommand(
    string DeckId,
    Guid CardId,
    Dictionary<MagicCardField, object?> Updates);

public class UpdateCardCommandHandler
{
    private readonly ICardStore<MagicCardPhysical> _cardStore;

    public UpdateCardCommandHandler(ICardStore<MagicCardPhysical> cardStore)
    {
        _cardStore = cardStore;
    }

    /// <summary>
    /// Updates specified fields on a card instance in a deck.
    /// Only non-null updates are applied.
    /// Returns true if the card was found and updated.
    /// </summary>
    public async Task<bool> Handle(UpdateCardCommand command, CancellationToken ct = default)
    {
        var location = Location.FromPath(command.DeckId);

        // Get existing instance
        var existing = await _cardStore.GetCardsAsync(command.CardId, ct);
        var matching = existing.FirstOrDefault(e => e.Location.Id == command.DeckId && e.Card.Id == command.CardId);

        if (matching == null)
            return false;

        // Apply updates
        var fieldsToUpdate = new List<MagicCardField>();

        foreach (var (field, value) in command.Updates)
        {
            switch (field)
            {
                case MagicCardField.Count when value is int count:
                    matching.Count = count;
                    fieldsToUpdate.Add(field);
                    break;
                case MagicCardField.IsOwn when value is bool isOwn:
                    matching.IsOwn = isOwn;
                    fieldsToUpdate.Add(field);
                    break;
                case MagicCardField.MyPrice when value is float price:
                    matching.MyPrice = price;
                    fieldsToUpdate.Add(field);
                    break;
                case MagicCardField.Comment when value is string comment:
                    matching.Comment = comment;
                    fieldsToUpdate.Add(field);
                    break;
                case MagicCardField.Special when value is string special:
                    matching.SpecialTags = special;
                    fieldsToUpdate.Add(field);
                    break;
                case MagicCardField.Custom when value is string custom:
                    matching.Custom = custom;
                    fieldsToUpdate.Add(field);
                    break;
            }
        }

        if (fieldsToUpdate.Any())
            await _cardStore.UpdateAsync(matching, fieldsToUpdate, ct);

        return true;
    }
}
