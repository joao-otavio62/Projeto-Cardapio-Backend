namespace PizzaFlow.Domain.ValueObjects;

public static class Cpf
{
    public static string Normalize(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());

    public static bool IsValid(string? value)
    {
        var d = Normalize(value);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
        return d[9] - '0' == CheckDigit(d, 9) && d[10] - '0' == CheckDigit(d, 10);
    }

    private static int CheckDigit(string d, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++) sum += (d[i] - '0') * (length + 1 - i);
        var r = sum * 10 % 11;
        return r == 10 ? 0 : r;
    }

    /// <summary>12345678909 -> ***.456.***-**</summary>
    public static string Mask(string cpf)
    {
        var d = Normalize(cpf);
        return d.Length == 11 ? $"***.{d.Substring(3, 3)}.***-**" : "***";
    }
}
