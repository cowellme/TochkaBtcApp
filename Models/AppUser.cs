using TochkaBtcApp.Telegram;

namespace TochkaBtcApp.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string ApiBinance { get; set; } = string.Empty;
        public string SecretBinance { get; set; } = string.Empty;
        public string ApiBingx { get; set; } = string.Empty;
        public string SecretBingx { get; set; } = string.Empty;
        public string ApiBitUnix { get; set; } = string.Empty;
        public string SecretBitUnix { get; set; } = string.Empty;
        public bool IsTelegram { get; set; } = false;
        public long TelegramId { get; set; }
        public bool? TelegramAlert { get; set; }
        public string Hash { get; set; } = string.Empty;

        public async void SendAlert(string message) => await TBot.SendMessageById(TelegramId, message);

        public async Task SaveSignal(Signal signal)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var db = new ApplicationContext();
                    db.Signals.Add(signal);
                    db.SaveChanges();
                });
            }
            catch (Exception e)
            {
                Error.Log(e);
            }
        }

        public async Task<List<Signal>?> GetSignals()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var db = new ApplicationContext();
                    var signals = db.Signals.Where(x => x.Owner == Hash).ToList();
                    return signals;
                }
                catch (Exception e)
                {
                    Error.Log(e);
                    return null;
                }
            });
        }

        public async Task DeleteSignal(Signal signal)
        {
            await using var db = new ApplicationContext();
            db.Signals.Remove(signal);
            await db.SaveChangesAsync();
        }

        public async Task UpdateSignal(Signal signal)
        {
            await using var db = new ApplicationContext();
            db.Signals.Update(signal);
            await db.SaveChangesAsync();
        }
    }
}
