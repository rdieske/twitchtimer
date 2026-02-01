using Microsoft.AspNetCore.Http;

namespace TwitchStreamTimer.Web.Services;

public interface IUserContext
{
    string? GetCurrentUserId();
    string? GetCurrentUserName();
    void SetUser(string userId, string userName);
    void ClearUser();
    bool IsAuthenticated { get; }
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
}
