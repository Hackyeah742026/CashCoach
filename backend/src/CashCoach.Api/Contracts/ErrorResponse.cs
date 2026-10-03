namespace CashCoach.Api.Contracts;

public sealed record ErrorResponse(ErrorDetail Error);

public sealed record ErrorDetail(string Code, string Message);
