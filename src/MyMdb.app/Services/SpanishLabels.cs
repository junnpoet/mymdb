using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace MyMdb.app.Services;

internal static class SpanishLabels
{
    private static readonly FrozenDictionary<string, string> Genres =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Action"] = "Acción",
            ["Adventure"] = "Aventura",
            ["Animation"] = "Animación",
            ["Biography"] = "Biografía",
            ["Comedy"] = "Comedia",
            ["Crime"] = "Crimen",
            ["Documentary"] = "Documental",
            ["Drama"] = "Drama",
            ["Family"] = "Familia",
            ["Fantasy"] = "Fantasía",
            ["Horror"] = "Terror",
            ["Music"] = "Música",
            ["Mystery"] = "Misterio",
            ["Romance"] = "Romance",
            ["Sci-Fi"] = "Ciencia ficción",
            ["Thriller"] = "Suspenso",
            ["War"] = "Bélica",
            ["Western"] = "Western",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> Languages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["English"] = "Inglés",
            ["Spanish"] = "Español",
            ["French"] = "Francés",
            ["German"] = "Alemán",
            ["Italian"] = "Italiano",
            ["Japanese"] = "Japonés",
            ["Korean"] = "Coreano",
            ["Mandarin"] = "Mandarín",
            ["Portuguese"] = "Portugués",
            ["Russian"] = "Ruso",
            ["Cantonese"] = "Cantonés",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string Genre(string canonical) =>
        Genres.TryGetValue(canonical, out var label) ? label : canonical;

    public static string Language(string canonical) =>
        Languages.TryGetValue(canonical, out var label) ? label : canonical;
}
