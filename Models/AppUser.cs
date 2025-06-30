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
        public string ApiOKX { get; set; } = string.Empty;
        public string SecretOKX { get; set; } = string.Empty;
        public string PhraseOKX { get; set; } = string.Empty;
        public bool IsTelegram { get; set; } = false;
        public long TelegramId { get; set; }
        public string Hash { get; set; } = string.Empty;

        public decimal GetBalance()
        {
            try
            {

                return 0;
            }
            catch (Exception e)
            {
                Error.Log(e);
                return 0;
            }
        }
    }
}
