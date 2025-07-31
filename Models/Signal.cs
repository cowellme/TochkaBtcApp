namespace TochkaBtcApp.Models;

public class Signal
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Owner { get; set; }
    public string? Description { get; set; }
    public string Symbol { get; set; }
    public string Data { get; set; }
    public bool IsActive { get; set; }
    public bool SingleWork { get; set; }
    public bool IsCompleted { get; set; }
}