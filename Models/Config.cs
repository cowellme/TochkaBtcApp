namespace TochkaBtcApp.Models
{
    public class Config
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string TimeFrame { get; set; }
        public int CandlesCount { get; set; } 
        public double RiskRatio { get; set; }
        public double OffsetMinimal { get; set; } 
        public double Risk { get; set; }
    }
}
