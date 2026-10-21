using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MagicAssistant.Core;

/// <summary>
/// Pure logic behind the set-icon stylesheet: turns a downloaded Scryfall SVG into the
/// `.set-symbol-&lt;code&gt;` rule that sets.css already carries. No filesystem access here —
/// every step is unit-testable, and the console tool (tools/SetIconsCssGen) shares this code.
///
/// Format note: icons are MASKS painted by the gold gradient of the base `.set-symbol` rule,
/// so the property is `mask-image`, never `background-image`. Verified byte-identical against
/// all 1047 historical rules (2026-10-21).
/// </summary>
public static partial class SetIconsCss
{
    /// <summary>Max accepted SVG size on disk; anything bigger is not an icon and must not bloat the stylesheet.</summary>
    public const int MaxSvgBytes = 64 * 1024;

    /// <summary>One set icon rule: class name + canonical base64 payload.</summary>
    public readonly record struct IconRule(string Code, string ClassName, string Base64)
    {
        public string ToCss() =>
            $".{ClassName} {{\n  mask-image: url(\"data:image/svg+xml;base64,{Base64}\");\n}}";
    }

    /// <summary>Scryfall set codes are lowercase alphanumeric (afr, 10e, znr). Anything else would
    /// produce a broken CSS identifier, so it is rejected rather than escaped.</summary>
    public static bool IsValidSetCode(string code) =>
        !string.IsNullOrEmpty(code) && code.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9');

    public static string ClassNameFor(string setCode) => $"set-symbol-{setCode.ToLowerInvariant()}";

    /// <summary>Canonical base64 of the raw file bytes: single line, standard alphabet, padding kept.</summary>
    public static string ToBase64(ReadOnlySpan<byte> svgBytes) => Convert.ToBase64String(svgBytes);

    public static string Sha256Hex(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>Checksum of the base64 TEXT (ASCII bytes) — the value compared against sets.css.</summary>
    public static string Sha256OfBase64(string base64) => Sha256Hex(Encoding.ASCII.GetBytes(base64));

    /// <summary>Rejects empty, oversized and non-SVG payloads; returns null when the file is usable.</summary>
    public static string? ValidateSvg(byte[] svgBytes)
    {
        if (svgBytes.Length == 0)
            return "empty file";
        if (svgBytes.Length > MaxSvgBytes)
            return $"too big ({svgBytes.Length} bytes > {MaxSvgBytes})";

        var head = Encoding.UTF8.GetString(svgBytes, 0, Math.Min(svgBytes.Length, 512));
        if (!head.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return "no <svg> element in the first 512 bytes";

        return null;
    }

    public static IconRule BuildRule(string setCode, byte[] svgBytes)
    {
        var code = setCode.ToLowerInvariant();
        if (!IsValidSetCode(code))
            throw new ArgumentException($"'{setCode}' is not a valid CSS class suffix (a-z0-9 only).");

        var problem = ValidateSvg(svgBytes);
        if (problem != null)
            throw new ArgumentException($"'{setCode}': {problem}");

        return new IconRule(code, ClassNameFor(code), ToBase64(svgBytes));
    }

    // ── sets.css parsing ────────────────────────────────────────────────────

    /// <summary>Grabs (code → base64) from an existing stylesheet. mask-image and background-image are
    /// both accepted because the historical file predates the mask convention.</summary>
    public static Dictionary<string, string> ReadExistingRules(string cssText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in RuleRegex().Matches(cssText))
            result[m.Groups[1].Value] = m.Groups[2].Value;
        return result;
    }

    public static HashSet<string> ReadExistingCodes(string cssText) =>
        new(ReadExistingRules(cssText).Keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>Codes defined more than once — a sanity check after any append.</summary>
    public static List<string> FindDuplicateCodes(string cssText)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dupes = new List<string>();
        foreach (Match m in RuleRegex().Matches(cssText))
            if (!seen.Add(m.Groups[1].Value))
                dupes.Add(m.Groups[1].Value);
        return dupes;
    }

    [GeneratedRegex(@"\.set-symbol-([a-z0-9]+)\s*\{[^}]*?(?:mask|background)-image:\s*url\(""data:image/svg\+xml;base64,([A-Za-z0-9+/=]+)""\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex RuleRegex();
}

/// <summary>Outcome of one stylesheet refresh.</summary>
public sealed record SetIconsCssResult(int RulesAdded, IReadOnlyList<string> Codes, string? Error = null)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Keeps sets.css in step with the downloaded icon cache: every `&lt;dataPath&gt;/set_icons/&lt;code&gt;.svg`
/// without a matching `.set-symbol-&lt;code&gt;` rule gets one appended. Self-healing by design — a fresh
/// deployment over its wwwroot rebuilds every missing rule from the data directory, no re-download.
///
/// Appends only; the hand-maintained head of the file (and the base .set-symbol rule) is never rewritten.
/// </summary>
public sealed class SetIconsCssGenerator
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly string _cssPath;
    private readonly string _iconsDir;

    public SetIconsCssGenerator(string cssPath, string iconsDir)
    {
        _cssPath = Path.GetFullPath(cssPath);
        _iconsDir = Path.GetFullPath(iconsDir);
    }

    public string CssPath => _cssPath;
    public string IconsDir => _iconsDir;

    /// <summary>Adds the missing rules. Never throws: a stylesheet problem must not fail a sets sync
    /// whose downloads already succeeded — the caller reports <see cref="SetIconsCssResult.Error"/> instead.</summary>
    public SetIconsCssResult SyncMissing(bool dryRun = false)
    {
        try
        {
            if (!File.Exists(_cssPath))
                return new SetIconsCssResult(0, Array.Empty<string>(), $"stylesheet not found: {_cssPath}");
            if (!Directory.Exists(_iconsDir))
                return new SetIconsCssResult(0, Array.Empty<string>(), $"icon cache not found: {_iconsDir}");

            WriteLock.Wait();
            try
            {
                var cssText = File.ReadAllText(_cssPath);
                var existing = SetIconsCss.ReadExistingCodes(cssText);

                var added = new List<SetIconsCss.IconRule>();
                foreach (var svgPath in Directory.EnumerateFiles(_iconsDir, "*.svg").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var code = Path.GetFileNameWithoutExtension(svgPath).ToLowerInvariant();
                    if (!SetIconsCss.IsValidSetCode(code) || existing.Contains(code))
                        continue;

                    var bytes = File.ReadAllBytes(svgPath);
                    if (SetIconsCss.ValidateSvg(bytes) != null)
                        continue;   // not an icon — leave it out of the stylesheet

                    added.Add(SetIconsCss.BuildRule(code, bytes));
                }

                if (added.Count == 0)
                    return new SetIconsCssResult(0, Array.Empty<string>());

                if (dryRun)
                    return new SetIconsCssResult(added.Count, added.Select(r => r.Code).ToArray());

                var block = string.Join("\n\n", added.Select(r => r.ToCss())) + "\n";

                // Temp file + move: a crash mid-write must not truncate a 2.8 MB stylesheet.
                // LF only, no BOM — same bytes as the hand-maintained file.
                var tmp = _cssPath + ".tmp";
                File.WriteAllText(tmp, cssText.TrimEnd('\n') + "\n\n" + block, new UTF8Encoding(false));
                File.Move(tmp, _cssPath, true);

                return new SetIconsCssResult(added.Count, added.Select(r => r.Code).ToArray());
            }
            finally
            {
                WriteLock.Release();
            }
        }
        catch (Exception ex)
        {
            return new SetIconsCssResult(0, Array.Empty<string>(), ex.Message);
        }
    }
}
