using System.Data;
using Dapper;

namespace MagicAssistant.Data.Db;

/// <summary>
/// Type handler for Guid columns in SQLite.
/// SQLite stores GUIDs as TEXT (uniqueidentifier), but Dapper's default reader
/// returns object which only supports long/double/string/byte[].
/// This handler bridges the gap by parsing the string back to Guid.
///
/// Required for all QueryAsync&lt;T&gt; calls that map Guid properties from SQLite rows.
/// Registered automatically via static constructor — no manual registration needed.
/// </summary>
public class SqliteGuidHandler : SqlMapper.TypeHandler<Guid>
{
    /// <summary>
    /// Static constructor registers this handler with Dapper once when the type is first used.
    /// This ensures it's available in both application and test contexts without manual setup.
    /// </summary>
    static SqliteGuidHandler()
        => SqlMapper.AddTypeHandler(new SqliteGuidHandler());

    public override void SetValue(IDbDataParameter parameter, Guid value)
        => parameter.Value = value.ToString();

    public override Guid Parse(object value)
        => value is Guid g ? g : Guid.Parse((string)value);
}
