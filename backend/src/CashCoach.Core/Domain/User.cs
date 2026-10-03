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

    /// <summary>Day of month the salary arrives (1 to 31). Detected on import; the user can override it.</summary>
    public int? Payday { get; set; }

    /// <summary>Money the forecast keeps untouched, in grosze.</summary>
    public long SafetyBufferGr { get; set; } = DefaultSafetyBufferGr;

    /// <summary>Account balance at the latest transaction date, in grosze; <c>null</c> until set (CSV exports carry no balance).</summary>
    public long? BalanceGr { get; set; }
}
