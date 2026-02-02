namespace TwitchStreamTimer.Web.Services;

public class UserData
{
    public string UserId { get; set; } = string.Empty;
    public string OverlayToken { get; set; } = GenerateShortToken();
    public DateTimeOffset TokenCreatedAt { get; set; } = DateTimeOffset.UtcNow;

    private static string GenerateShortToken()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }

    public void RegenerateToken()
    {
        OverlayToken = GenerateShortToken();
        TokenCreatedAt = DateTimeOffset.UtcNow;
    }
}
