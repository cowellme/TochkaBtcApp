using Microsoft.EntityFrameworkCore;
using TochkaBtcApp.Components;
using TochkaBtcApp.Models;

public class ApplicationContext : DbContext
{
    private string _connectoinString = "server=127.0.0.1;uid=root;pwd=asde1D#cEC;database=tochka;";
    public ApplicationContext(bool reset = false)
    {
        if (reset)
            Database.EnsureDeleted();
        Database.EnsureCreated();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseMySql(_connectoinString, new MySqlServerVersion(new Version(8, 0, 20)));
    //db pass: asde1D#cEC
    
    public DbSet<AppUser> Users { get; set; }
    public DbSet<Config> Configs { get; set; }
    public DbSet<Error> Errors { get; set; }


    public void Reset()
    {
        using var db = new ApplicationContext();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        db.SaveChanges();
    }
    public bool SaveUser(AppUser newUser)
    {
        
        try
        {
            using var db = new ApplicationContext();
            var existingUser = db.Users.ToList().FirstOrDefault(u => u.Name == newUser.Name);
            newUser.Hash = Guid.NewGuid().ToString();

            if (existingUser == null)
            {
                db.Users.Add(newUser); // Добавляем нового пользователя
            }
            else
            {
                db.Users.Update(newUser); // Обновляем существующего
            }

            db.SaveChanges();
            return true;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return false;
        }
    }
    public static List<AppUser>? GetUsers()
    {
        try
        {
            using var db = new ApplicationContext();
            return db.Users.ToList();
        }
        catch (Exception e)
        {

            Error.Log(e);
            return null;
        }
    }
    public static AppUser GetAppUserByIp(string ip)
    {
        try
        {
            var users = GetUsers();

            if (users != null)
            {
                var user = users.FirstOrDefault(x => x.Ip == ip) ?? new AppUser { Ip = ip };

                return user;
            }

            return new AppUser { Ip = ip };
        }
        catch (Exception e)
        {
            Error.Log(e);
            return new AppUser{ Ip = ip };
        }
    }
    public static bool SaveConfig(Config newConfig)
    {
        try
        {
            using var db = new ApplicationContext();
            db.Configs.Add(newConfig);
            db.SaveChanges();
            return true;
        }
        catch (Exception e)
        {
            Error.Log(e);
            return false;
        }
    }
    public static List<Config>? GetConfigsByName(string name)
    {
        try
        {
            using var db = new ApplicationContext();
            return db.Configs.Where(x => x.Name == name).ToList();
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
    public static List<Config>? GetConfigs()
    {
        try
        {
            using var db = new ApplicationContext();
            return db.Configs.ToList();
        }
        catch (Exception e)
        {
            Error.Log(e);
            return null;
        }
    }
    public static void SaveError(Error error)
    {
        using var db = new ApplicationContext();
        db.Errors.Add(error);
        db.SaveChanges();
    }
    public async Task DeleteConfig(int configId, string configName)
    {
        await using var db = new ApplicationContext();
        var delConfig = db.Configs.FirstOrDefault(x => x.Id == configId);

        if (delConfig != null)
        {
            db.Configs.Remove(delConfig);
            await db.SaveChangesAsync();
        }
    }
    public static async Task ClearError()
    {
        await using var db = new ApplicationContext();
        var range = db.Errors.ToList();
        db.Errors.RemoveRange(range);
        await db.SaveChangesAsync();
    }
}