using MyMdb.Core.Parsing;

namespace MyMdb.Core.Tests;

public sealed class EmdbRealDatabaseTests
{
    [Fact]
    public void Parse_RealEmdbDatabase_ParsesAllEntriesAndMatchesKnownRecords()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "emdb.dat");
        Assert.True(File.Exists(path), $"Test fixture not found at '{path}'.");

        var parser = new EmdbParser();
        using var stream = File.OpenRead(path);

        var catalog = parser.Parse(stream);

        Assert.Equal("80", catalog.Version);
        Assert.True(catalog.Entries.Count >= 350, $"Expected at least 350 entries, found {catalog.Entries.Count}.");
        Assert.NotEmpty(catalog.Collections);
        Assert.All(catalog.Entries, entry =>
        {
            Assert.True(entry.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(entry.Title));
        });

        var movie358 = Assert.Single(catalog.Entries, e => e.Id == 358);
        Assert.Equal("Lo dejamos acá", movie358.Title);
        Assert.Equal(2026, movie358.Year);
        Assert.Equal(97, movie358.RuntimeMinutes);
        Assert.Equal("AR", movie358.CountryCode);
        Assert.Equal("Kenya Films", movie358.Studio);
        Assert.Equal(["Hernán Goldfrid"], movie358.Directors);
        Assert.Equal(["Emanuel Diez"], movie358.Writers);
        Assert.Equal(["Roque Baños"], movie358.Composers);
        Assert.Equal(["Comedy", "Drama"], movie358.Genres);
        Assert.Equal(["Spanish"], movie358.Languages);
        Assert.Equal(5, movie358.Cast.Count);
        Assert.Equal("Ricardo Darín", movie358.Cast[0].Name);
        Assert.Equal("Ulises Verón", movie358.Cast[0].Character);
        Assert.Equal("https://www.netflix.com/title/82027563", movie358.WebLink);

        var breakingBad = Assert.Single(catalog.Entries, e => e.Id == 5);
        Assert.Equal("Breaking Bad", breakingBad.Title);
        Assert.True(breakingBad.IsTvSeries);
        Assert.Equal("01-05", breakingBad.SeasonsRange);
        Assert.Equal(9.5, breakingBad.ImdbRating);
        Assert.True(breakingBad.IsFavorite);

        var mommy = Assert.Single(catalog.Entries, e => e.Id == 253);
        Assert.False(mommy.IsTvSeries);

        var winaypacha = Assert.Single(catalog.Entries, e => e.Id == 93);
        Assert.False(winaypacha.IsTvSeries);

        var theBoys = Assert.Single(catalog.Entries, e => e.Id == 18);
        Assert.True(theBoys.IsTvSeries);
        Assert.Equal("01-04", theBoys.SeasonsRange);
    }
}
