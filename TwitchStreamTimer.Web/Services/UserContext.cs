using Microsoft.AspNetCore.Http;

namespace TwitchStreamTimer.Web.Services;

public interface IUserContext
{
    string? GetCurrentUserId();
    string? GetCurrentUserName();
    void SetUser(string userId, string userName);
    void ClearUser();
    bool IsAuthenticated { get; }
    
    string GetCurrentUserToken();
    string? GetUserIdByToken(string token);
    void RegenerateToken();
}

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string UserIdKey = "TwitchUserId";
    private const string UserNameKey = "TwitchUserName";

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.Session.GetString(UserIdKey);
    }

    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.Session.GetString(UserNameKey);
    }

    public void SetUser(string userId, string userName)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.SetString(UserIdKey, userId);
            session.SetString(UserNameKey, userName);
        }
    }

    public void ClearUser()
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            session.Remove(UserIdKey);
            session.Remove(UserNameKey);
        }
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(GetCurrentUserId());

    private string GetUserDataPath(string userId)
    {
        var dataDir = Path.Combine("Data", userId);
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "user_data.json");
    }

    private UserData LoadUserData(string userId)
    {
        var path = GetUserDataPath(userId);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return System.Text.Json.JsonSerializer.Deserialize<UserData>(json) ?? new UserData { UserId = userId };
        }
        return new UserData { UserId = userId };
    }

    private void SaveUserData(UserData userData)
    {
        var path = GetUserDataPath(userData.UserId);
        var json = System.Text.Json.JsonSerializer.Serialize(userData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public string GetCurrentUserToken()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return string.Empty;
        
        var userData = LoadUserData(userId);
        return userData.OverlayToken;
    }

    public string? GetUserIdByToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var dataDir = "Data";
        if (!Directory.Exists(dataDir)) return null;

        foreach (var userDir in Directory.GetDirectories(dataDir))
        {
            var userDataPath = Path.Combine(userDir, "user_data.json");
            if (File.Exists(userDataPath))
            {
                try
                {
                    var json = File.ReadAllText(userDataPath);
                    var userData = System.Text.Json.JsonSerializer.Deserialize<UserData>(json);
                    if (userData?.OverlayToken == token)
                    {
                        return userData.UserId;
                    }
                }
                catch { /* Skip invalid files */ }
            }
        }

        return null;
    }

    public void RegenerateToken()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return;

        var userData = LoadUserData(userId);
        userData.RegenerateToken();
        SaveUserData(userData);
    }
}
