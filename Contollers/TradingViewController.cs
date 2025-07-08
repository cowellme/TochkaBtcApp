using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.Mvc;
using OKX.Net.Clients;
using OKX.Net.Enums;
using OKX.Net.Objects.Market;
using TochkaBtcApp.Models;
using TochkaBtcApp.Models.Exc;
using OrderSide = OKX.Net.Enums.OrderSide;

namespace TochkaBtcApp.Contollers
{
    [ApiController]
    [Route("[controller]")]
    public class TradingViewController : ControllerBase
    {

        [HttpPost("signal")]
        public async Task<IActionResult> Signal([FromBody] dynamic alert)
        {
            try
            {
                //16.05.2025 6:49:02: Data: {"text": "Buy"}
                string str = $"Data: {alert}";
                await System.IO.File.AppendAllTextAsync("log.tmp", $"{DateTime.Now}: {str}\n");

                if (str.Contains("Long 5m BitUnix"))
                {
                    var bitUnix = new BitUnix();
                    var result = await bitUnix.GetSignal(GlobalKlineInterval.FiveMinutes);

                    return Ok(new
                    {
                        status = "Success", 
                        data = result
                    });
                }

                return Ok(new { status = "Success", data = alert });
            }
            catch (Exception e)
            {
                Error.Log(e);
                return BadRequest(e.Message);
            }
        }
    } 
    //'ex6rYpDE58dY0I8ehgwm4Urd0XhDSCDEMiNulWVKJvWl3pQ7bchR5F9Iwccm2OKP'
    //'8itSLGUpnFrdRdKMqfVTptNstEqunC1wZbEHtde2H778JhMdqSSYBorbJk6CRmlN'
    public class Exchange
    {
    }

    internal class RLogger  
    {
        public static void Error(Exception exception)
        {
            
        }
    }

    
}
