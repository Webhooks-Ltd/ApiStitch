using System.Text;

namespace ApiStitch.Parsing;

internal static class NamingHelper
{
    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length);
        var capitalizeNext = true;

        foreach (var c in input)
        {
            if (c is '_' or '-' or '.' or ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0 && char.IsLower(sb[0]))
            sb[0] = char.ToUpperInvariant(sb[0]);

        return sb.ToString();
    }

    public static string ResolveCollision(string baseName, HashSet<string> usedNames)
    {
        if (usedNames.Add(baseName))
            return baseName;

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName}{i}";
            if (usedNames.Add(candidate))
                return candidate;
        }
    }
}
