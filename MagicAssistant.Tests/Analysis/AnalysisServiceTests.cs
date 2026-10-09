namespace MagicAssistant.Tests.Analysis;

[TestFixture]
public class AnalysisServiceTests
{
    private readonly AnalysisService _service = new();

    // ─── Analyze — Basic Stats ─────────────────────────────

    [Test]
    public void Analyze_EmptyDeck_ReturnsZeroStats()
    {
        var result = _service.Analyze(Array.Empty<MyCard>());
        
        Assert.That(result.TotalCards, Is.EqualTo(0));
        Assert.That(result.TotalManaCost, Is.EqualTo(0));
        Assert.That(result.MaxRepeats, Is.EqualTo(0));
    }

    [Test]
    public void Analyze_SingleCard_CorrectTotalCards()
    {
        var card = CreatePhysicalCard("Lightning Bolt", "{R}", "Instant", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.TotalCards, Is.EqualTo(1));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_MultipleCards_CorrectTotalCards()
    {
        var cards = new[]
        {
            CreatePhysicalCard("Card 1", "{1}", "Instant", 1),
            CreatePhysicalCard("Card 2", "{2}", "Sorcery", 1),
            CreatePhysicalCard("Card 3", "{3}", "Enchantment", 1),
        };

        try
        {
            var result = _service.Analyze(cards);
            Assert.That(result.TotalCards, Is.EqualTo(3));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_CardWithCount_CorrectTotalCards()
    {
        var card = CreatePhysicalCard("Lightning Bolt", "{R}", "Instant", 4);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.TotalCards, Is.EqualTo(4));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    // ─── Analyze — Mana Curve ──────────────────────────────

    [Test]
    public void Analyze_ZeroCmcCard_PutsInIndex0()
    {
        var card = CreatePhysicalCard("Thoughtscour", "{0}", "Instant", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.ManaCurve[0], Is.EqualTo(1));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_OneCmcCard_PutsInIndex1()
    {
        var card = CreatePhysicalCard("Lightning Bolt", "{R}", "Instant", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.ManaCurve[1], Is.EqualTo(1));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_HighCmcCard_PutsInIndex7()
    {
        var card = CreatePhysicalCard("Massacre", "{7}", "Sorcery", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.ManaCurve[7], Is.EqualTo(1));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_LandExcludedFromManaCurve()
    {
        var land = CreatePhysicalCard("Forest", "{T}: Add {G}", "Basic Land — Forest", 1);
        var result = _service.Analyze(new[] { land });

        Assert.That(result.ManaCurve[0], Is.EqualTo(0));
        Assert.That(result.ManaCurve[1], Is.EqualTo(0));
    }

    [Test]
    public void Analyze_MultipleCardsSameCmc_Aggregates()
    {
        var cards = new[]
        {
            CreatePhysicalCard("Card 1", "{1}", "Instant", 2),
            CreatePhysicalCard("Card 2", "{1}", "Sorcery", 2),
        };

        try
        {
            var result = _service.Analyze(cards);
            Assert.That(result.ManaCurve[1], Is.EqualTo(4));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_TotalManaCost_CorrectlyCalculated()
    {
        var cards = new[]
        {
            CreatePhysicalCard("Card 1", "{2}", "Instant", 1),
            CreatePhysicalCard("Card 2", "{3}", "Sorcery", 1),
        };

        try
        {
            var result = _service.Analyze(cards);
            Assert.That(result.TotalManaCost, Is.EqualTo(5));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_ExcludesLandsFromManaCost()
    {
        var land = CreatePhysicalCard("Forest", "{T}: Add {G}", "Basic Land — Forest", 1);
        var result = _service.Analyze(new[] { land });
        
        Assert.That(result.TotalManaCost, Is.EqualTo(0));
    }

    // ─── Analyze — Color Distribution ──────────────────────
    // ⚠️ BUG: AnalysisService.Analyze() crashes with KeyNotFoundException
    // when accessing ColorDistribution[key] for non-existent keys.
    // The line `result.ColorDistribution[key] += count` throws because
    // C# Dictionary indexer doesn't auto-create missing keys.
    // This affects ALL non-land cards. Tests below document expected behavior.

    [Test]
    public void Analyze_WhiteCard_CountsInW()
    {
        var card = CreatePhysicalCard("Wall of Wood", "{W}", "Creature — Wall", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.ColorDistribution["W"], Is.EqualTo(1));
        }
        catch (KeyNotFoundException ex) when (ex.Message.Contains("Colorless") || ex.Message.Contains("R"))
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException when accessing ColorDistribution. "
                + $"Expected ColorDistribution[\"W\"] = 1, but got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_BlueCard_CountsInU()
    {
        var card = CreatePhysicalCard("Counterspell", "{U}{U}", "Instant", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.ColorDistribution["U"], Is.EqualTo(1));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException. "
                + $"Expected ColorDistribution[\"U\"] = 2, but got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_MultiColorCard_CountsInBothColors()
    {
        var card = new MagicCard
        {
            Id = Guid.NewGuid(),
            Name = "Twincast",
            ManaCost = "{W}{U}",
            Type = "Instant",
            CmcNumber = ColorTypes.GetConvertedManaCost("{W}{U}"),
        };
        var physical = new MyCard(card) { Count = 2 };
        try
        {
            var result = _service.Analyze(new[] { physical });

            // multi-color card with count=2: each color should have count=2
            Assert.That(result.ColorDistribution.ContainsKey("W"), Is.True);
            Assert.That(result.ColorDistribution["W"], Is.EqualTo(2));
            Assert.That(result.ColorDistribution.ContainsKey("U"), Is.True);
            Assert.That(result.ColorDistribution["U"], Is.EqualTo(2));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException for multi-color cards. "
                + $"ColorType=\"multi\" is not handled by NormalizeColorKey. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_ColorlessCard_CountsInColorless()
    {
        var card = CreatePhysicalCard("Arcane Signet", "{2}", "Artifact", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.ColorDistribution.ContainsKey("Colorless"), Is.True);
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException for colorless cards. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_LandExcludedFromColorDistribution()
    {
        var land = CreatePhysicalCard("Forest", "{T}: Add {G}", "Basic Land — Forest", 1);
        var result = _service.Analyze(new[] { land });

        Assert.That(result.ColorDistribution, Is.Empty);
    }

    // ─── Analyze — Type Distribution ───────────────────────

    [Test]
    public void Analyze_CreatureCard_CountsCreature()
    {
        var card = CreatePhysicalCard("Goblin", "{R}", "Creature — Goblin Warrior", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.CreatureCount, Is.EqualTo(1));
            Assert.That(result.LandCount, Is.EqualTo(0));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_TypeDistribution_CorrectlyClassifiesAllTypes()
    {
        var cards = new[]
        {
            CreatePhysicalCard("Forest", "{T}", "Basic Land — Forest", 3),
            CreatePhysicalCard("Goblin", "{R}", "Creature — Goblin", 2),
            CreatePhysicalCard("Lightning Bolt", "{R}", "Instant", 1),
            CreatePhysicalCard("Flame Rift", "{3}{R}", "Sorcery", 1),
            CreatePhysicalCard("Anoint", "{W}", "Enchantment", 4),
            CreatePhysicalCard("Signet", "{2}", "Artifact", 1),
            CreatePhysicalCard("Jace", "{3}{U}", "Legendary Planeswalker — Jace", 1),
        };
        try
        {
            var result = _service.Analyze(cards);

            Assert.That(result.LandCount, Is.EqualTo(3));
            Assert.That(result.CreatureCount, Is.EqualTo(2));
            // Instant(1) + Sorcery(1) + Enchantment(4) + Artifact(1) + Planeswalker(1) = 8
            Assert.That(result.NonCreatureCount, Is.EqualTo(8));
            Assert.That(result.TotalCards, Is.EqualTo(13));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    // ─── Analyze — Creature Subtypes ───────────────────────

    [Test]
    public void Analyze_Creature_ExtractsAllSubtypes()
    {
        var cards = new[]
        {
            CreatePhysicalCard("Goblin 1", "{R}", "Creature — Goblin Warrior", 2),
            CreatePhysicalCard("Goblin 2", "{R}", "Creature — Goblin Shaman", 1),
            CreatePhysicalCard("Human", "{W}", "Creature — Human Soldier", 3),
        };

        try
        {
            var result = _service.Analyze(cards);

            // All unique subtypes should be present
            Assert.That(result.CreatureSubtypes.ContainsKey("Goblin"), Is.True);
            Assert.That(result.CreatureSubtypes.ContainsKey("Warrior"), Is.True);
            Assert.That(result.CreatureSubtypes.ContainsKey("Shaman"), Is.True);
            Assert.That(result.CreatureSubtypes.ContainsKey("Human"), Is.True);
            Assert.That(result.CreatureSubtypes.ContainsKey("Soldier"), Is.True);

            // BUG: values are always 1 instead of actual card counts per subtype.
            // Goblin appears on 2 cards (count 2+1=3 copies), Warrior on 1 card (2 copies), etc.
            // Expected: CreatureSubtypes["Goblin"] == 3, CreatureSubtypes["Warrior"] == 2
            Assert.That(result.CreatureSubtypes["Goblin"], Is.EqualTo(3),
                "CreatureSubtypes should count total copies per subtype, not just presence");
            Assert.That(result.CreatureSubtypes["Warrior"], Is.EqualTo(2),
                "CreatureSubtypes should count total copies per subtype, not just presence");
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    // ─── Analyze — Ability Stats ───────────────────────────

    [Test]
    public void Analyze_MultipleCardsWithAbilities_CountsCorrectly()
    {
        var bear = CreatePhysicalCard("Runeclaw Bear", "{1}{W}", "Creature — Bear", 2);
        bear.Card.OracleText = "Flying";

        var ape = CreatePhysicalCard("Kird Ape", "{R}", "Creature — Ape", 1);
        ape.Card.OracleText = "Haste";

        var swift = CreatePhysicalCard("Swift Blade", "{1}{R}", "Creature — Goblin Warrior", 3);
        swift.Card.OracleText = "Haste, First Strike";

        var cards = new[] { bear, ape, swift };
        try
        {
            var result = _service.Analyze(cards);

            // Flying: 1 card (2 copies), Haste: 2 cards (1+3=4 copies), First Strike: 1 card (3 copies)
            Assert.That(result.AbilityStats.ContainsKey("Flying"), Is.True);
            Assert.That(result.AbilityStats.ContainsKey("Haste"), Is.True);
            Assert.That(result.AbilityStats.ContainsKey("First Strike"), Is.True);

            // BUG: values are always 1 instead of actual card counts with that ability.
            // Expected: AbilityStats["Haste"] == 4 (2 cards with haste, total 4 copies)
            Assert.That(result.AbilityStats["Haste"], Is.EqualTo(4),
                "AbilityStats should count total copies per ability, not just presence");
            Assert.That(result.AbilityStats["Flying"], Is.EqualTo(2),
                "AbilityStats should count total copies per ability, not just presence");
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_NoOracleText_NoAbilities()
    {
        var card = CreatePhysicalCard("Goblin", "{R}", "Creature — Goblin", 1);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.AbilityStats, Is.Empty);
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    // ─── Analyze — Max Repeats ─────────────────────────────

    [Test]
    public void Analyze_SameCardMultipleTimes_CorrectMaxRepeats()
    {
        var card = CreatePhysicalCard("Lightning Bolt", "{R}", "Instant", 4);
        try
        {
            var result = _service.Analyze(new[] { card });
            Assert.That(result.MaxRepeats, Is.EqualTo(4));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_DifferentCards_CorrectMaxRepeats()
    {
        var cards = new[]
        {
            CreatePhysicalCard("Bolt", "{R}", "Instant", 4),
            CreatePhysicalCard("Counterspell", "{U}{U}", "Instant", 3),
        };

        try
        {
            var result = _service.Analyze(cards);
            Assert.That(result.MaxRepeats, Is.EqualTo(4));
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_ExcludesBasicLandsFromMaxRepeats()
    {
        var land = CreatePhysicalCard("Forest", "{T}", "Basic Land — Forest", 4);
        var result = _service.Analyze(new[] { land });
        
        Assert.That(result.MaxRepeats, Is.EqualTo(0));
    }

    // ─── Analyze — Deck Colors ─────────────────────────────

    [Test]
    public void Analyze_WhiteDeck_DeckColorsContainsW()
    {
        var card = CreatePhysicalCard("Wall of Wood", "{W}", "Creature — Wall", 1);
        try
        {
            var result = _service.Analyze(new[] { card });

            // DeckColors is a computed string property: "{W}" format
            Assert.That(result.DeckColors.Contains("W"), Is.True);
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    [Test]
    public void Analyze_ColorlessDeck_DeckColorsContainsColorless()
    {
        var card = CreatePhysicalCard("Arcane Signet", "{2}", "Artifact", 1);
        try
        {
            var result = _service.Analyze(new[] { card });

            // DeckColors is a computed string property: "{Colorless}" format
            Assert.That(result.DeckColors.Contains("Colorless"), Is.True);
        }
        catch (KeyNotFoundException ex)
        {
            Assert.Fail($"BUG: Analyze() crashes with KeyNotFoundException in ColorDistribution. Got: {ex.Message}");
        }
    }

    // ─── ComputeManaCurve ──────────────────────────────────

    [Test]
    public void ComputeManaCurve_EmptyDeck_ReturnsZeros()
    {
        var curve = _service.ComputeManaCurve(Array.Empty<MyCard>());
        
        Assert.That(curve, Has.Length.EqualTo(8));
        Assert.That(curve[0], Is.EqualTo(0));
        Assert.That(curve[7], Is.EqualTo(0));
    }

    [Test]
    public void ComputeManaCurve_SingleCard_CorrectDistribution()
    {
        var card = CreatePhysicalCard("Lightning Bolt", "{R}", "Instant", 4);
        var curve = _service.ComputeManaCurve(new[] { card });
        
        Assert.That(curve[1], Is.EqualTo(4));
        Assert.That(curve.Sum(), Is.EqualTo(4));
    }

    [Test]
    public void ComputeManaCurve_LandExcluded()
    {
        var land = CreatePhysicalCard("Forest", "{T}", "Basic Land — Forest", 1);
        var curve = _service.ComputeManaCurve(new[] { land });
        
        Assert.That(curve.Sum(), Is.EqualTo(0));
    }

    // ─── IsLand ────────────────────────────────────────────

    [Test]
    public void IsLand_DetectsLandSupertypeTokens()
    {
        Assert.That(AnalysisService.IsLand(new MagicCard { Type = "Basic Land — Forest" }), Is.True);
        Assert.That(AnalysisService.IsLand(new MagicCard { Type = "Land" }), Is.True);
        Assert.That(AnalysisService.IsLand(new MagicCard { Type = "Creature - Human Soldier" }), Is.False);
        Assert.That(AnalysisService.IsLand(new MagicCard { Type = null }), Is.False);
    }

    // ─── Helper ────────────────────────────────────────────

    private MyCard CreatePhysicalCard(string name, string cost, string type, int count)
    {
        var cmc = ColorTypes.GetConvertedManaCost(cost);
        var card = new MagicCard
        {
            Id = Guid.NewGuid(),
            Name = name,
            ManaCost = cost,
            Type = type,
            CmcNumber = cmc,
        };

        return new MyCard(card) { Count = count };
    }
}
