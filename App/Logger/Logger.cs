namespace TochkaBtcApp.App.Logger
{
    
    public class Logger
    {
        public static async Task WriteLog(string str)
        {
            await File.AppendAllTextAsync("log.tmp", $"{DateTime.Now}: {str}\n");
        }
    }
}
