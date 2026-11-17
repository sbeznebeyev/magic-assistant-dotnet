namespace MagicAssistant.Data.Model;

/// <summary>
/// Physical card condition grade. Persisted as a nullable integer in Deck.Grade
/// (NULL = not graded). Values are stable — they are stored in the database.
/// </summary>
public enum CardGrade
{
    /// <summary>M — GEM MINT / MINT</summary>
    GemMint = 0,

    /// <summary>NM — NEAR MINT</summary>
    NearMint = 1,

    /// <summary>SP — SLIGHTLY PLAYED</summary>
    SlightlyPlayed = 2,

    /// <summary>MP — MODERATELY PLAYED</summary>
    ModeratelyPlayed = 3,

    /// <summary>HP — HEAVILY PLAYED</summary>
    HeavilyPlayed = 4,

    /// <summary>DMG — DAMAGED</summary>
    Damaged = 5
}
