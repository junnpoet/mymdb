using System.Text;
using MyMdb.Core.Parsing;

namespace MyMdb.Core.Tests;

public sealed class EmdbParserTests
{
    private const char Rs = '\u001e';

    [Fact]
    public void Parse_MovieEntry_DecodesAllFieldsCorrectly()
    {
        var json = $$"""
        {
            "version": "80",
            "collections": [{ "id": "726871", "name": "Dune Collection", "sort-string": "Dune" }],
            "actors": [
                { "id": "tmdb-1190668", "name": "Timothée Chalamet" },
                { "id": "tmdb-933238", "name": "Rebecca Ferguson" }
            ],
            "directors": [{ "name": "Denis Villeneuve" }],
            "writers": [{ "name": "Jon Spaihts" }],
            "composers": [{ "name": "Hans Zimmer" }],
            "movies": [{
                "title": "Dune: Parte uno{{Rs}}#ODune: Part One{{Rs}}Legendary Pictures{{Rs}}726871",
                "year": "2021{{Rs}}0{{Rs}}155{{Rs}}#US{{Rs}}0{{Rs}}0{{Rs}}@0{{Rs}}https://example.com/dune{{Rs}}-1{{Rs}}512{{Rs}}1{{Rs}}0{{Rs}}0{{Rs}}0{{Rs}}1{{Rs}}0{{Rs}}0",
                "genres": "SV{{Rs}}1160419|0{{Rs}}20240415{{Rs}}B{{Rs}}1@0@0{{Rs}}{{Rs}}0{{Rs}}www.youtube.com/watch?v=abc{{Rs}}20240415{{Rs}}/m/dune_2021{{Rs}}438631",
                "cast": "0,1{{Rs}}Paul Atreides|Lady Jessica|||",
                "plot": "La <DQ>especia melange<DQ> es vital.",
                "tagline": "Más allá del miedo.{{Rs}}{{Rs}}https://www.netflix.com/title/1",
                "rating": "438{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}ME{{Rs}}{{Rs}}846031{{Rs}}0{{Rs}}90183074{{Rs}}",
                "comments": "Gran sonido"
            }]
        }
        """;

        var parser = new EmdbParser();
        using var stream = ToUtf16LeStream(json);

        var catalog = parser.Parse(stream);

        Assert.Equal("80", catalog.Version);
        Assert.Single(catalog.Collections);
        var movie = Assert.Single(catalog.Entries);

        Assert.Equal(1, movie.Id);
        Assert.Equal("000001.jpg", movie.MediaFileName);
        Assert.Equal("Dune: Parte uno", movie.Title);
        Assert.Equal("Dune: Part One", movie.OriginalTitle);
        Assert.Equal("Legendary Pictures", movie.Studio);
        Assert.Equal("726871", movie.CollectionId);
        Assert.Equal(2021, movie.Year);
        Assert.Equal(155, movie.RuntimeMinutes);
        Assert.Equal("US", movie.CountryCode);
        Assert.False(movie.IsTvSeries);
        Assert.Equal(["Sci-Fi", "Adventure"], movie.Genres);
        Assert.Equal(["Mandarin", "English"], movie.Languages);
        Assert.Equal("B", movie.Certification);
        Assert.Equal(new DateOnly(2024, 4, 15), movie.DateAdded);
        Assert.Equal(new DateOnly(2024, 4, 15), movie.DateLastWatched);
        Assert.Equal(2, movie.SeenCount);
        Assert.False(movie.IsFavorite);
        Assert.Equal(["Denis Villeneuve"], movie.Directors);
        Assert.Equal(["Jon Spaihts"], movie.Writers);
        Assert.Equal(["Hans Zimmer"], movie.Composers);
        Assert.Equal("La \"especia melange\" es vital.", movie.Plot);
        Assert.Equal("Más allá del miedo.", movie.Tagline);
        Assert.Equal("Gran sonido", movie.Comments);
        Assert.Equal(8.0, movie.ImdbRating);
        Assert.Equal(846031, movie.ImdbVotes);
        Assert.Equal(74, movie.Metascore);
        Assert.Equal("tt1160419", movie.ImdbId);
        Assert.Equal("438631", movie.TmdbId);
        Assert.Equal("https://www.netflix.com/title/1", movie.WebLink);
        Assert.Equal("https://example.com/dune", movie.Location);

        Assert.Equal(2, movie.Cast.Count);
        Assert.Equal("Timothée Chalamet", movie.Cast[0].Name);
        Assert.Equal("Paul Atreides", movie.Cast[0].Character);
        Assert.Equal("tmdb-1190668.jpg", movie.Cast[0].PhotoFileName);
        Assert.Null(movie.Cast[0].EpisodeCount);
    }

    [Fact]
    public void Parse_TvSeriesEntry_DecodesSeriesFlagsFavoriteAndEpisodes()
    {
        var json = $$"""
        {
            "version": "80",
            "actors": [{ "id": "tmdb-17419", "name": "Bryan Cranston" }],
            "movies": [{
                "title": "Breaking Bad{{Rs}}{{Rs}}AMC{{Rs}}-1",
                "year": "2008{{Rs}}{{Rs}}4418{{Rs}}#US{{Rs}}0{{Rs}}0{{Rs}}@0{{Rs}}{{Rs}}-1{{Rs}}288{{Rs}}1{{Rs}}0{{Rs}}0{{Rs}}0{{Rs}}5{{Rs}}{{Rs}}",
                "genres": "D!#{{Rs}}0903747|0{{Rs}}20240420{{Rs}}C{{Rs}}01-05@1@94{{Rs}}{{Rs}}0{{Rs}}{{Rs}}20240420{{Rs}}/tv/breaking_bad{{Rs}}1396",
                "cast": "0{{Rs}}Walter White#64|||",
                "plot": "A chemistry teacher turns to crime.",
                "tagline": "Recuerda mi nombre{{Rs}}{{Rs}}",
                "rating": "447{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}EGS{{Rs}}{{Rs}}2132218{{Rs}}0{{Rs}}999999000{{Rs}}"
            }]
        }
        """;

        var parser = new EmdbParser();
        using var stream = ToUtf16LeStream(json);

        var entry = Assert.Single(parser.Parse(stream).Entries);

        Assert.Equal(5, entry.Id);
        Assert.Equal("000005.jpg", entry.MediaFileName);
        Assert.True(entry.IsTvSeries);
        Assert.Equal("01-05", entry.SeasonsRange);
        Assert.Equal(94, entry.EpisodeCount);
        Assert.Equal(1, entry.SeenCount);
        Assert.True(entry.IsFavorite);
        Assert.Equal(9.5, entry.ImdbRating);
        Assert.Null(entry.Metascore);
        Assert.Null(entry.CollectionId);

        var lead = Assert.Single(entry.Cast);
        Assert.Equal("Walter White", lead.Character);
        Assert.Equal(64, lead.EpisodeCount);
    }

    [Fact]
    public void Parse_UnwatchedMovieWithoutRating_ReturnsNullOptionalFields()
    {
        var json = $$"""
        {
            "version": "80",
            "movies": [{
                "title": "Lo dejamos acá{{Rs}}{{Rs}}Kenya Films{{Rs}}-1",
                "year": "2026{{Rs}}{{Rs}}97{{Rs}}#AR{{Rs}}0{{Rs}}0{{Rs}}@0{{Rs}}{{Rs}}-1{{Rs}}0{{Rs}}1{{Rs}}0{{Rs}}0{{Rs}}0{{Rs}}358{{Rs}}{{Rs}}",
                "genres": "KD{{Rs}}37027309|0{{Rs}}20261030{{Rs}}{{Rs}}1@0@0{{Rs}}{{Rs}}0{{Rs}}www.youtube.com/watch?v=DgZQwmjdcp8{{Rs}}{{Rs}}/m/therapy_done_right{{Rs}}1487317",
                "cast": "{{Rs}}",
                "plot": "Un psicoanalista de prestigio.",
                "tagline": "{{Rs}}{{Rs}}https://www.netflix.com/title/82027563",
                "rating": "3E8{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}S{{Rs}}{{Rs}}0{{Rs}}0{{Rs}}999999000{{Rs}}"
            }]
        }
        """;

        var parser = new EmdbParser();
        using var stream = ToUtf16LeStream(json);

        var entry = Assert.Single(parser.Parse(stream).Entries);

        Assert.Equal(358, entry.Id);
        Assert.Equal("000358.jpg", entry.MediaFileName);
        Assert.Equal("AR", entry.CountryCode);
        Assert.Equal(["Comedy", "Drama"], entry.Genres);
        Assert.Equal(["Spanish"], entry.Languages);
        Assert.Equal(0, entry.SeenCount);
        Assert.False(entry.IsFavorite);
        Assert.Null(entry.DateLastWatched);
        Assert.Null(entry.ImdbRating);
        Assert.Null(entry.ImdbVotes);
        Assert.Null(entry.Comments);
        Assert.Equal("https://www.netflix.com/title/82027563", entry.WebLink);
    }

    [Fact]
    public void Parse_MovieWithStatusOneAndOngoingTvSeries_DetectsTvSeriesByHashMarker()
    {
        var json = $$"""
        {
            "version": "80",
            "movies": [
                {
                    "title": "Mommy{{Rs}}{{Rs}}{{Rs}}-1",
                    "year": "2014{{Rs}}{{Rs}}139{{Rs}}#CA{{Rs}}0{{Rs}}0{{Rs}}@0{{Rs}}{{Rs}}-1{{Rs}}256{{Rs}}1{{Rs}}0{{Rs}}0{{Rs}}0{{Rs}}253{{Rs}}{{Rs}}",
                    "genres": "D{{Rs}}3612616|0{{Rs}}20250101{{Rs}}{{Rs}}1@1@0{{Rs}}{{Rs}}0{{Rs}}{{Rs}}20250101{{Rs}}{{Rs}}265177",
                    "cast": "{{Rs}}",
                    "plot": "",
                    "tagline": "{{Rs}}{{Rs}}",
                    "rating": "439{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}F{{Rs}}{{Rs}}60000{{Rs}}0{{Rs}}999999074{{Rs}}"
                },
                {
                    "title": "The Boys{{Rs}}{{Rs}}Amazon{{Rs}}-1",
                    "year": "2019{{Rs}}{{Rs}}4941{{Rs}}#US{{Rs}}0{{Rs}}0{{Rs}}@0{{Rs}}{{Rs}}-1{{Rs}}256{{Rs}}1{{Rs}}0{{Rs}}0{{Rs}}0{{Rs}}18{{Rs}}{{Rs}}",
                    "genres": "#{{Rs}}1190634|0{{Rs}}20240501{{Rs}}{{Rs}}01-04@2@81{{Rs}}{{Rs}}0{{Rs}}{{Rs}}20240501{{Rs}}{{Rs}}76479",
                    "cast": "{{Rs}}",
                    "plot": "",
                    "tagline": "{{Rs}}{{Rs}}",
                    "rating": "43F{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}E{{Rs}}{{Rs}}700000{{Rs}}0{{Rs}}999999000{{Rs}}"
                }
            ]
        }
        """;

        var parser = new EmdbParser();
        using var stream = ToUtf16LeStream(json);

        var entries = parser.Parse(stream).Entries;

        var mommy = Assert.Single(entries, e => e.Id == 253);
        Assert.False(mommy.IsTvSeries);
        Assert.Null(mommy.SeasonsRange);
        Assert.Null(mommy.EpisodeCount);

        var theBoys = Assert.Single(entries, e => e.Id == 18);
        Assert.True(theBoys.IsTvSeries);
        Assert.Equal("01-04", theBoys.SeasonsRange);
        Assert.Equal(81, theBoys.EpisodeCount);
        Assert.Empty(theBoys.Genres);
    }

    private static MemoryStream ToUtf16LeStream(string text)
    {
        var preamble = Encoding.Unicode.GetPreamble();
        var body = Encoding.Unicode.GetBytes(text);
        var buffer = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, buffer, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, buffer, preamble.Length, body.Length);
        return new MemoryStream(buffer);
    }
}
