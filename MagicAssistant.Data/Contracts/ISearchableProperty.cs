
namespace MagicAssistant.Data.Contracts;

public interface ISearchableProperty
{
    string GetFilterField();
    IEnumerable<string> GetIds();
    string GetNameById(string id);
}
