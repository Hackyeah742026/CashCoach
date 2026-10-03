namespace CashCoach.Core.Domain;

public enum Persona
{
    Student,
    FirstJob,
    BnplHeavy,

    /// <summary>A real user who imports their own CSV.</summary>
    Custom,
}
