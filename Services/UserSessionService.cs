// Services/UserSessionService.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using System.Net;
using TochkaBtcApp.Models;

namespace YourApp.Services;

public class UserSessionService : IDisposable
{
    private readonly IPersistentStorage _storage;
    private readonly NavigationManager _nav;
    private readonly IHttpContextAccessor _httpContext;

    public AppUser CurrentUser { get; private set; }

    public UserSessionService(
        IPersistentStorage storage,
        NavigationManager nav,
        IHttpContextAccessor httpContext)
    {
        _storage = storage;
        _nav = nav;
        _httpContext = httpContext;
        InitializeUser();
    }

    private void InitializeUser()
    {
        var ip = GetUserIp();
        if (ip == null) return;

        CurrentUser = _storage.GetUser(ip) ?? CreateNewUser(ip);
    }

    private AppUser CreateNewUser(string ip)
    {
        var newUser = new AppUser
        {
            Ip = ip,
        };

        _storage.SaveUser(newUser);
        return newUser;
    }

    private string? GetUserIp()
    {
        return _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }

    public void UpdateLastActive()
    {
        if (CurrentUser != null)
        {
            _storage.SaveUser(CurrentUser);
        }
    }

    public void Dispose()
    {
        UpdateLastActive();
    }
}