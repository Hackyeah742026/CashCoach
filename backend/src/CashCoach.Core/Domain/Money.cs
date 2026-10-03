namespace CashCoach.Core.Domain;

public static class Money
{
    /// <summary>Grosze to złoty with a scale of 2, so JSON renders <c>-11.00</c> rather than <c>-11</c>.</summary>
    public static decimal ToZloty(long grosze) => grosze * 0.01m;

    public static long Round(decimal grosze) => (long)decimal.Round(grosze, MidpointRounding.AwayFromZero);
}
