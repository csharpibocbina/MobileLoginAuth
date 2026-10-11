namespace MobileLoginAuth.Application.Options;

public class LockoutOptions
{
    public const string SectionName = "Lockout";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
