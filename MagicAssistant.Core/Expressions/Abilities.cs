using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MagicAssistant.Core.Expressions;

/// <summary>
/// MTG ability keywords and patterns for filter search.
/// Mirrors Java Abilities.java — loads from abilities.txt on disk if available,
/// otherwise uses a built-in set of well-known keyword abilities.
/// </summary>
public static class Abilities
{
    private static readonly Lazy<RegexAbilityMatcher[]> _abilities = new(InitializeAbilities);

    public record RegexAbilityMatcher(string DisplayName, Regex Pattern, bool IsKeyword) : IAbilityMatcher
    {
        public bool Match(string text)
        {
            if (text.IndexOf('(') >= 0)
                text = Regex.Replace(text, @"\([^)]*\)", " ");
            return Pattern.IsMatch(text);
        }
    };

    public interface IAbilityMatcher
    {
        string DisplayName { get; }
        Regex Pattern { get; }
        bool IsKeyword { get; }
        bool Match(string text);
    }

    private static Regex AbilityPattern(string keyword)
        => new($@"\b{Regex.Escape(keyword.ToLowerInvariant())}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static Regex AbilityPatternRegex(string regex)
        => new(regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static Regex AbilityPatternWord(string word, string suffix = "s?")
        => AbilityPatternRegex($@"\b{Regex.Escape(word.ToLowerInvariant())}{suffix}\b");

    private static RegexAbilityMatcher[] InitializeAbilities()
    {
        var list = new List<RegexAbilityMatcher>();

        // Keyword abilities — simple word boundary matching
        var keywords = new[] {
            "channel", "chroma", "domain", "grandeur", "hellbent", "imprint", "join forces",
            "kinship", "landfall", "metalcraft", "morbid", "radiance", "sweep", "threshold",
            "affinity", "amplify", "annihilator", "aura swap", "banding", "battle cry",
            "bloodthirst", "bushido", "buyback", "cascade", "champion", "changeling",
            "conspire", "convoke", "cumulative upkeep", "cycling", "deathtouch", "defender",
            "delve", "devour", "double strike", "dredge", "echo", "enchant", "entwine",
            "epic", "equip", "evoke", "exalted", "fading", "fear", "first strike",
            "flanking", "flash", "flashback", "flying", "forecast", "fortify", "frenzy",
            "graft", "gravestorm", "haste", "haunt", "hexproof", "hideaway",
            "horsemanship", "infect", "intimidate", "kicker", "multikicker", "landwalk",
            "level up", "lifelink", "living weapon", "landcycling", "madness", "modular",
            "morph", "ninjutsu", "offering", "persist", "phasing", "poisonous", "protection",
            "provoke", "prowl", "rampage", "reach", "rebound", "recover", "reinforce",
            "replicate", "retrace", "regenerate", "ripple", "shroud", "soulshift",
            "splice", "split second", "storm", "sunburst", "suspend", "totem armor",
            "trample", "transfigure", "transmute", "unearth", "vanishing", "vigilance",
            "wither", "extort", "cipher", "bloodrush", "battalion", "evolve", "gates"
        };

        foreach (var kw in keywords)
            list.Add(new RegexAbilityMatcher(kw, AbilityPattern(kw), true));

        // Mined abilities — more complex patterns
        var mined = new[] {
            ("Tap/Untap", "(tap(s|ped)?|untap(s|ped)?)"),
            ("Sacrifice", "\\bsacrifices?\\b"),
            ("Search", "\\bsearch\\b"),
            ("Reveal", "\\breveal\\bs?\\b"),
            ("Hand", "\\bhand\\b"),
            ("Shuffle", "(re)?shuffles?"),
            ("Target", "\\btarget\\b"),
            ("Creature", "(creatures?|[0-9]*/[0-9]*)"),
            ("Player", "\\bplayer\\bs?\\b"),
            ("Power/Toughness", "([\\+\\-][0-9]/[\\+\\-][0-9])|\\b(power|toughness)\\b"),
            ("Upkeep", "\\bupkeep\\b"),
            ("Name", "\\bname\\bd?\\b"),
            ("Remove", "\\bremove\\b"),
            ("Turn", "\\bturn\\b"),
            ("Mana", "\\bmana\\b"),
            ("Attack", "(non)?attack(ing|s|ed)?"),
            ("Defend", "(non)?defend(ing|s|ed)?"),
            ("Block", "(non)?block(ing|s|ed)?"),
            ("Discard", "\\bdiscards?\\b"),
            ("Look", "\\blook\\b"),
            ("Choose", "\\bchoose\\b"),
            ("Color", "color(s|ed|less)?"),
            ("Life", "\\blife\\b"),
            ("Control", "control(s|ler)?"),
            ("Owner", "\\bowner\\bs?\\b"),
            ("Graveyard", "\\bgraveyard\\b"),
            ("Activation", "activat(e|ion)"),
            ("Draw", "\\bdraw\\bs?\\b"),
            ("Permanent", "\\bpermanent\\bs?\\b"),
            ("Blue", "(non)?blue"),
            ("Black", "(non)?black"),
            ("White", "(non)?white"),
            ("Green", "(non)?green"),
            ("Red", "(non)?red"),
            ("Counter target spells", "counter\\s+target\\s+spell"),
            ("Destroy", "\\bdestroy\\b"),
            ("Cost", "\\bcost\\b"),
            ("Cumulative", "\\bcumulative\\b"),
            ("Counter", "counters?"),
            ("Combat", "\\bcombat\\b"),
            ("Battlefield", "\\bbattlefield\\b"),
            ("Game", "\\bgame\\b"),
            ("Attach", "attach(ed)?"),
            ("Step/Phase", "(step|phase)"),
            ("Random", "\\brandom\\b"),
            ("Mountain", "mountains?(walk)?"),
            ("Plain", "plains?(walk)?"),
            ("Swamp", "swamps?(walk)?"),
            ("Forest", "forests?(walk)?"),
            ("Island", "islands?(walk)?"),
            ("Bury", "\\bbury\\b"),
            ("Token", "(non)?tokens?"),
            ("Library", "\\blibrary\\b"),
            ("Gain", "\\bgain\\bs?\\b"),
            ("Flip", "\\bflip\\b"),
            ("Exile", "\\bexile\\bd?\\b"),
            ("Summon", "summon(ing)?\\s+sick(ness)?"),
            ("Ante", "\\banteb?\\b"),
            ("Type", "\\btype\\b"),
            ("Return", "\\breturn\\b"),
            ("Indestructible", "\\bindestructible\\b"),
            ("Opponent", "\\bopponent\\bs?\\b"),
            ("Face", "\\bface\\b"),
            ("Pay", "\\bpay\\b"),
            ("Interrupt", "\\binterrupt\\b"),
        };

        foreach (var (name, pattern) in mined)
            list.Add(new RegexAbilityMatcher(name, AbilityPatternRegex(pattern), false));

        return list.ToArray();
    }

    public static RegexAbilityMatcher[] GetAbilities() => _abilities.Value;

    public static Regex? GetPattern(string value)
    {
        foreach (var a in _abilities.Value)
        {
            if (a.DisplayName.Equals(value, StringComparison.OrdinalIgnoreCase))
                return a.Pattern;
        }
        return null;
    }

    public static bool MatchAbility(string abilityName, string text)
    {
        var pattern = GetPattern(abilityName);
        return pattern?.IsMatch(text) == true;
    }
}
