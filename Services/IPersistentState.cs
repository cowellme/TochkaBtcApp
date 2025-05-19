// Services/IPersistentStorage.cs
using Microsoft.Extensions.Caching.Memory;
using TochkaBtcApp.Models;

namespace YourApp.Services;

public interface IPersistentStorage
{
    AppUser? GetUser(string ip);
    void SaveUser(AppUser user);
}

public class MemoryPersistentStorage : IPersistentStorage
{
    private readonly IMemoryCache _cache;

    public MemoryPersistentStorage(IMemoryCache cache)
    {
        _cache = cache;
    }

    public AppUser? GetUser(string ip)
    {
        return _cache.Get<AppUser>($"user_{ip}");
    }

    public void SaveUser(AppUser user)
    {
        _cache.Set($"user_{user.Ip}", user,
            new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromDays(30) });
    }
}