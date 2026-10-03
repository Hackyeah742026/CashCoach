namespace CashCoach.Core.Domain;

public enum IncomeStatus
{
    /// <summary>The user has not answered yet; the payday is a guess from the imported data.</summary>
    Unknown,

    /// <summary>The user confirmed or entered their regular income.</summary>
    Confirmed,

    /// <summary>The user said they have no regular income.</summary>
    None,
}

public enum PaydayRule
{
    /// <summary>The same day of month (clamped to short months).</summary>
    FixedDay,

    /// <summary>The last Monday-to-Friday of the month.</summary>
    LastWorkingDay,
}
