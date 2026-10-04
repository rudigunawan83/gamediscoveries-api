namespace GameDiscoveries.Modules.Users.Features.Register;

public sealed record RegisterCommand(string Email, string Password, string DisplayName);
