using CalendarWork.Core.Calendar;
using CalendarWork.Core.Holidays;

namespace CalendarWork.Core.Tests;

public class HolidayProviderTests
{
    private static HolidayProvider Load()
    {
        var provider = new HolidayProvider();
        provider.Load(HolidayProvider.LoadEmbedded());
        return provider;
    }

    [Fact]
    public void Embedded_Contains2026And2027()
    {
        var provider = Load();
        Assert.True(provider.GetVersion(2026) > 0);
        Assert.True(provider.GetVersion(2027) > 0);
    }

    [Fact]
    public void Substitute_2026SamilJeol()
    {
        Assert.True(Load().TryGet(new DateOnly(2026, 3, 2), out var h));
        Assert.Equal(HolidayKind.Substitute, h.Kind);
    }

    [Theory]
    [InlineData(2026)]
    [InlineData(2027)]
    public void LunarHolidays_MatchConverter(int year)
    {
        var provider = Load();
        Assert.True(LunarConverter.TryToSolar(year, 1, 1, false, out var seollal));
        Assert.True(LunarConverter.TryToSolar(year, 4, 8, false, out var buddha));
        Assert.True(LunarConverter.TryToSolar(year, 8, 15, false, out var chuseok));

        foreach (var d in new[] { seollal.AddDays(-1), seollal, seollal.AddDays(1), buddha, chuseok.AddDays(-1), chuseok, chuseok.AddDays(1) })
            Assert.True(provider.TryGet(d, out _), $"{d} 공휴일 누락");
    }

    [Fact]
    public void Load_HigherVersionWins()
    {
        var provider = new HolidayProvider();
        provider.Load([
            new HolidayFile { Year = 2030, Version = 1, Items = [new(new DateOnly(2030, 1, 1), "신정", HolidayKind.Public)] },
            new HolidayFile { Year = 2030, Version = 2, Items = [new(new DateOnly(2030, 1, 2), "임시", HolidayKind.Temporary)] },
        ]);
        Assert.False(provider.TryGet(new DateOnly(2030, 1, 1), out _));
        Assert.True(provider.TryGet(new DateOnly(2030, 1, 2), out _));
        Assert.Equal(2, provider.GetVersion(2030));
    }

    [Fact]
    public void Load_MergesSameDayNames()
    {
        var provider = new HolidayProvider();
        provider.Load([
            new HolidayFile { Year = 2025, Version = 1, Items = [
                new(new DateOnly(2025, 5, 5), "어린이날", HolidayKind.Public),
                new(new DateOnly(2025, 5, 5), "부처님오신날", HolidayKind.Public)] },
        ]);
        Assert.True(provider.TryGet(new DateOnly(2025, 5, 5), out var h));
        Assert.Equal("어린이날·부처님오신날", h.Name);
    }
}
