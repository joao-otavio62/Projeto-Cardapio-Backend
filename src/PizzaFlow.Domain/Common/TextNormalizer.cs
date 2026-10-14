using System.Globalization;
using System.Text;

namespace PizzaFlow.Domain.Common;

public static class TextNormalizer
{
    /// <summary>minúsculas + sem acentos ("Calabresa Ç" -> "calabresa c"). Base da busca fuzzy.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
