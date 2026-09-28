using System;

/// <summary>
/// A simple in-game date. Uses a fixed-length month/year so date arithmetic
/// (AddDays, comparisons) is cheap and predictable — no leap years, no real
/// calendar quirks. Tune DaysPerMonth/MonthsPerYear to taste.
///
/// AbsoluteDay is the important bit: it's a single int that uniquely
/// represents a date, so it can be used directly as a Dictionary key for
/// scheduled story events ("on this exact day, X happens").
/// </summary>
[Serializable]
public struct GameDate : IComparable<GameDate>, IEquatable<GameDate>
{
    public int year;
    public int month; // 1-based
    public int day;   // 1-based

    public const int DaysPerMonth = 30;
    public const int MonthsPerYear = 12;

    public GameDate(int year, int month, int day)
    {
        this.year = year;
        this.month = month;
        this.day = day;
    }

    public int AbsoluteDay =>
        (year - 1) * MonthsPerYear * DaysPerMonth + (month - 1) * DaysPerMonth + (day - 1);

    public static GameDate FromAbsoluteDay(int absoluteDay)
    {
        int daysPerYear = MonthsPerYear * DaysPerMonth;
        int year = absoluteDay / daysPerYear + 1;
        int remainder = absoluteDay % daysPerYear;
        int month = remainder / DaysPerMonth + 1;
        int day = remainder % DaysPerMonth + 1;
        return new GameDate(year, month, day);
    }

    public GameDate AddDays(int amount) => FromAbsoluteDay(AbsoluteDay + amount);

    public int CompareTo(GameDate other) => AbsoluteDay.CompareTo(other.AbsoluteDay);
    public bool Equals(GameDate other) => AbsoluteDay == other.AbsoluteDay;
    public override bool Equals(object obj) => obj is GameDate other && Equals(other);
    public override int GetHashCode() => AbsoluteDay;
    public override string ToString() => $"20XX / {month:00} / {day:00}";

    public static bool operator ==(GameDate a, GameDate b) => a.Equals(b);
    public static bool operator !=(GameDate a, GameDate b) => !a.Equals(b);
    public static bool operator <(GameDate a, GameDate b) => a.AbsoluteDay < b.AbsoluteDay;
    public static bool operator >(GameDate a, GameDate b) => a.AbsoluteDay > b.AbsoluteDay;
    public static bool operator <=(GameDate a, GameDate b) => a.AbsoluteDay <= b.AbsoluteDay;
    public static bool operator >=(GameDate a, GameDate b) => a.AbsoluteDay >= b.AbsoluteDay;
}