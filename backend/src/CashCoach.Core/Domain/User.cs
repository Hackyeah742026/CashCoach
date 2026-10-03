namespace CashCoach.Core.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Persona Persona { get; set; }
    public string Language { get; set; } = "pl";
    public DateTime? ConsentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
