using System.Collections.Frozen;

namespace MyMdb.Core.Parsing;

internal static class EmdbLookupTables
{
    private static readonly FrozenDictionary<char, string> GenreCodes = new Dictionary<char, string>
    {
        ['A'] = "Action",
        ['V'] = "Adventure",
        ['I'] = "Animation",
        ['G'] = "Biography",
        ['K'] = "Comedy",
        ['!'] = "Crime",
        ['U'] = "Documentary",
        ['D'] = "Drama",
        ['C'] = "Family",
        ['F'] = "Fantasy",
        ['H'] = "Horror",
        ['M'] = "Music",
        ['Y'] = "Mystery",
        ['R'] = "Romance",
        ['S'] = "Sci-Fi",
        ['T'] = "Thriller",
        ['#'] = "TV Series",
        ['O'] = "War",
        ['W'] = "Western",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<char, string> LanguageCodes = new Dictionary<char, string>
    {
        ['E'] = "English",
        ['S'] = "Spanish",
        ['F'] = "French",
        ['G'] = "German",
        ['I'] = "Italian",
        ['J'] = "Japanese",
        ['K'] = "Korean",
        ['M'] = "Mandarin",
        ['P'] = "Portuguese",
        ['R'] = "Russian",
        ['2'] = "Cantonese",
    }.ToFrozenDictionary();

    public static IReadOnlyList<string> DecodeGenres(string? codes)
    {
        if (string.IsNullOrEmpty(codes))
        {
            return [];
        }

        var result = new List<string>(codes.Length);
        foreach (var ch in codes)
        {
            if (GenreCodes.TryGetValue(ch, out var label))
            {
                result.Add(label);
            }
        }

        return result;
    }

    public static IReadOnlyList<string> DecodeLanguages(string? codes)
    {
        if (string.IsNullOrEmpty(codes))
        {
            return [];
        }

        var result = new List<string>(codes.Length);
        foreach (var ch in codes)
        {
            if (LanguageCodes.TryGetValue(ch, out var label))
            {
                result.Add(label);
            }
        }

        return result;
    }
}
