namespace CashCoach.Core.Domain;

public class User
{
    public const long DefaultSafetyBufferGr = 30_000;

    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Persona Persona { get; set; }
    public string Language { get; set; } = "pl";
    public DateTime? ConsentAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Day of month the income arrives (1 to 31; 31 with <see cref="PaydayRule.LastWorkingDay"/>). Guessed on import until confirmed.</summary>
    public int? Payday { get; set; }

    public PaydayRule PaydayRule { get; set; } = PaydayRule.FixedDay;

    /// <summary>Confirmed regular monthly income in grosze; <c>null</c> until confirmed.</summary>
    public long? SalaryGr { get; set; }

    public IncomeStatus IncomeStatus { get; set; } = IncomeStatus.Unknown;

    /// <summary>Where the income comes from, e.g. <c>Wynagrodzenie</c>.</summary>
    public string? IncomeSource { get; set; }

    /// <summary>Money the forecast keeps untouched, in grosze.</summary>
    public long SafetyBufferGr { get; set; } = DefaultSafetyBufferGr;

    /// <summary>Account balance at the latest transaction date, in grosze; <c>null</c> until set (CSV exports carry no balance).</summary>
    public long? BalanceGr { get; set; }
}
