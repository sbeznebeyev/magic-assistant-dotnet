namespace MagicAssistant.App.Shared;

/// <summary>
/// Numeric filter input sanitization (shared by CardFilterDialog Properties rows
/// and CardFilterAdvancedDialog collection ranges): strip all non-numeric characters
/// except a single dot. Replaces comma with dot (European input).
/// </summary>
public static class FilterInputSanitizer
{
    public static string SanitizeNumeric(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Replace comma with dot (European input)
        input = input.Replace(',', '.');

        var result = new System.Text.StringBuilder();
        bool hasDot = false;

        foreach (char c in input)
        {
            if (char.IsDigit(c))
                result.Append(c);
            else if (c == '.' && !hasDot)
            {
                result.Append('.');
                hasDot = true;
            }
            // All other characters are silently dropped
        }

        return result.ToString();
    }
}
