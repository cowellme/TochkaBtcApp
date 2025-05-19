namespace TochkaBtcApp.Models
{
    public class Error
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public string CreatedAt { get; set; }

        public static void Log(Exception exception)
        {
            ApplicationContext.SaveError(new Error
            {
                Message = $"Message: '{exception.Message}' Source:'{exception.Source}'",
                CreatedAt = $"{DateTime.Now:g}"
            });
        }


    }
}
