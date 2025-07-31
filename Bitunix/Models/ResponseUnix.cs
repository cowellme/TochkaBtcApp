namespace TochkaBtcApp.Bitunix.Models
{
    public class Account
    {
        public string marginCoin { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string margin { get; set; }
        public string transfer { get; set; }
        public string positionMode { get; set; }
        public string crossUnrealizedPNL { get; set; }
        public string isolationUnrealizedPNL { get; set; }
        public string bonus { get; set; }
    }

    public class ResponseAccount
    {
        public int code { get; set; }
        public Account data { get; set; }
        public string msg { get; set; }
    }
}
