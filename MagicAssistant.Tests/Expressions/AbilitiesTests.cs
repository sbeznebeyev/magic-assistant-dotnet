using MagicAssistant.Core.Expressions;

namespace MagicAssistant.Tests.Expressions;

[TestFixture]
public class AbilitiesTests
{
    [Test]
    public void GetAbilities_ReturnsNonEmptyConsistentList()
    {
        var abilities = Abilities.GetAbilities();
        Assert.That(abilities, Is.Not.Null.And.Not.Empty);

        // Consistent results on repeated calls (Lazy<T> guarantee)
        var second = Abilities.GetAbilities().ToList();
        Assert.That(second, Is.EqualTo(abilities.ToList()));
    }

    [Test]
    public void AllAbilities_HaveValidDisplayNameAndPattern()
    {
        var abilities = Abilities.GetAbilities();
        Assert.That(abilities, Is.Not.Empty);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ability in abilities)
        {
            Assert.That(ability.DisplayName, Is.Not.Null.And.Not.Empty,
                $"Ability has null or empty DisplayName");
            Assert.That(ability.Pattern, Is.Not.Null,
                $"Ability '{ability.DisplayName}' has null Pattern");

            // Check for duplicate display names (case-insensitive)
            Assert.That(names.Add(ability.DisplayName), Is.True,
                $"Duplicate ability name: '{ability.DisplayName}'");
        }

        // Verify well-known keywords are present (subset of ~150 abilities).
        // Keywords use lowercase DisplayName in the source code.
        // Note: "indestructible" is a mined ability, not a keyword — check separately.
        var requiredKeywords = new[]
        {
            "flying", "haste", "first strike", "deathtouch", "hexproof",
            "lifelink", "trample", "vigilance", "flash"
        };

        foreach (var keyword in requiredKeywords)
        {
            Assert.That(names, Does.Contain(keyword), $"Missing required keyword: {keyword}");
        }
    }

    [Test]
    public void KeywordAbilities_MatchInNaturalLanguageText()
    {
        var abilities = Abilities.GetAbilities().ToDictionary(a => a.DisplayName, StringComparer.OrdinalIgnoreCase);

        // Simple keyword abilities — match in natural language sentences
        var tests = new (string Name, string Text)[]
        {
            ("flying", "This creature has flying."),
            ("haste", "This creature has haste."),
            ("hexproof", "It has hexproof from its controller."),
            ("lifelink", "This creature has lifelink."),
            ("deathtouch", "This creature has deathtouch."),
            ("trample", "Creatures you control get +1/+1 and have trample."),
            ("vigilance", "They gain vigilance."),
            ("indestructible", "Creatures you control are indestructible."),
            ("flash", "Enchanted creature gets +1/+1 and gains flash."),
            ("first strike", "This creature has first strike."),
        };

        foreach (var (name, text) in tests)
        {
            Assert.That(abilities.ContainsKey(name), Is.True, $"Ability '{name}' not found");
            Assert.That(abilities[name].Match(text), Is.True,
                $"Keyword '{name}' should match: '{text}'");
        }
    }

    [Test]
    public void MinedAbilities_MatchInOracleText()
    {
        var abilities = Abilities.GetAbilities().ToDictionary(a => a.DisplayName, StringComparer.OrdinalIgnoreCase);

        // Mined abilities — match in realistic Oracle text fragments.
        // Use forms that the patterns can actually match (singular verbs where pattern is broken).
        var tests = new (string Name, string Text)[]
        {
            ("search", "Search your library for a basic land card."),
            ("target", "Target creature gets -2/-2 until end of turn."),
            ("draw", "You may draw a card."),
            ("destroy", "Destroy target artifact."),
            ("graveyard", "Return target creature card from a graveyard to the battlefield."),
            ("life", "You gain 3 life."),
            ("token", "Create a 1/1 white Soldier creature token."),
            ("exile", "Exile target permanent."),
            ("mana", "Add one mana of any color."),
        };

        foreach (var (name, text) in tests)
        {
            Assert.That(abilities.ContainsKey(name), Is.True, $"Ability '{name}' not found");
            Assert.That(abilities[name].Match(text), Is.True,
                $"Mined ability '{name}' should match: '{text}'");
        }
    }

    [Test]
    public void SacrificePattern_MatchesBothSingularAndPlural()
    {
        // BUG: The Sacrifice pattern is "\\bsacrifice\\bs?\\b" which has a word boundary
        // between "sacrifice" and "s?". This means it matches "sacrifice" but NOT "sacrifices".
        // Oracle text frequently uses "sacrifices" (e.g. "sacrifices a creature").
        var sacrifice = Abilities.GetAbilities().First(a => a.DisplayName == "Sacrifice");

        // Singular form — should work
        Assert.That(sacrifice.Match("Sacrifice a creature"), Is.True,
            "Sacrifice pattern should match 'sacrifice' (singular)");

        try
        {
            Assert.That(sacrifice.Match("It sacrifices a creature each turn"), Is.True,
                "Sacrifice pattern should match 'sacrifices' (plural/verb form)");
        }
        catch (AssertionException ex)
        {
            throw new AssertionException(
                $"BUG: Sacrifice regex pattern does not match 'sacrifices'. " +
                $"Pattern is '\\\\bsacrifice\\\\bs?\\\\b' — the word boundary between " +
                $"'sacrifice' and 's?' prevents matching the plural form. " +
                $"Oracle text commonly uses 'sacrifices' (third-person verb). " +
                $"Fix: Change pattern to '\\\\bsacrifices?\\\\b'.",
                ex);
        }
    }

    [Test]
    public void DiscardPattern_MatchesBothSingularAndPlural()
    {
        // BUG: Same issue as Sacrifice — Discard pattern is "\\bdiscard\\bs?\\b"
        // which matches "discard" but NOT "discards".
        var discard = Abilities.GetAbilities().First(a => a.DisplayName == "Discard");

        // Singular form — should work
        Assert.That(discard.Match("Discard a card"), Is.True,
            "Discard pattern should match 'discard' (singular)");

        try
        {
            Assert.That(discard.Match("Target player discards a card"), Is.True,
                "Discard pattern should match 'discards' (third-person verb form)");
        }
        catch (AssertionException ex)
        {
            throw new AssertionException(
                $"BUG: Discard regex pattern does not match 'discards'. " +
                $"Pattern is '\\\\bdiscard\\\\bs?\\\\b' — the word boundary between " +
                $"'discard' and 's?' prevents matching the plural form. " +
                $"Oracle text commonly uses 'discards' (third-person verb). " +
                $"Fix: Change pattern to '\\\\bdiscards?\\\\b'.",
                ex);
        }
    }

    [Test]
    public void RegexMatching_WithParentheses()
    {
        var flying = Abilities.GetAbilities().First(a => a.DisplayName == "flying");
        Assert.That(flying.Match("This creature has flying (keyword)"), Is.True,
            "Match should strip parentheses before matching");
    }

    [Test]
    public void Abilities_DoNotMatchUnrelatedText()
    {
        // Negative tests — abilities should NOT match unrelated text
        var abilities = Abilities.GetAbilities().ToDictionary(a => a.DisplayName, StringComparer.OrdinalIgnoreCase);

        Assert.That(abilities["flying"].Match("The mountain is very tall"), Is.False,
            "Flying should not match unrelated text");
        Assert.That(abilities["draw"].Match("The drawer opened smoothly"), Is.False,
            "Draw should not match 'drawer' (different word)");
    }

    [Test]
    public void GetAbilities_IsThreadSafe_LazyInit()
    {
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => Abilities.GetAbilities()))
            .ToArray();
        Task.WaitAll(tasks);

        // All results should be same reference (Lazy<T>)
        var first = tasks[0].Result;
        foreach (var task in tasks.Skip(1))
            Assert.That(task.Result, Is.SameAs(first));
    }

    [Test]
    public void GetPattern_FoundByName()
    {
        var pattern = Abilities.GetPattern("flying");
        Assert.That(pattern, Is.Not.Null, "GetPattern should find 'flying' by name");
        Assert.That(pattern!.IsMatch("This creature has flying"), Is.True);
    }

    [Test]
    public void GetPattern_ReturnsNull_WhenNotFound()
    {
        var pattern = Abilities.GetPattern("NonExistentAbility12345");
        Assert.That(pattern, Is.Null, "GetPattern should return null for unknown ability name");
    }

    [Test]
    public void MatchAbility_CaseInsensitiveLookup()
    {
        Assert.That(Abilities.MatchAbility("flying", "This creature has flying"), Is.True,
            "MatchAbility should be case-insensitive on ability name lookup");
        Assert.That(Abilities.MatchAbility("FLYING", "This creature has flying"), Is.True,
            "MatchAbility should handle uppercase ability name");
    }

    [Test]
    public void MatchAbility_ReturnsFalse_WhenPatternNotFound()
    {
        Assert.That(Abilities.MatchAbility("TotallyMadeUpKeyword", "some text"), Is.False,
            "MatchAbility should return false when ability pattern is not found");
    }
}
