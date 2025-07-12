using CryptoExchange.Net.Authentication;
using Microsoft.AspNetCore.Mvc;
using OKX.Net.Clients;
using OKX.Net.Enums;
using OKX.Net.Objects.Market;
using TochkaBtcApp.App.Logger;
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
                string str = $"{DateTime.Now:g}: {alert}";
                IExchange? exchange= null;
                GlobalKlineInterval? interval = null;

                if (str.Contains("Long 5m BitUnix"))
                {
                    exchange = new BitUnix();
                    interval = GlobalKlineInterval.FiveMinutes;
                }
                if (str.Contains("Long 15m BitUnix"))
                {
                    exchange = new BitUnix(); 
                    interval = GlobalKlineInterval.FifteenMinutes;
                }
                if (str.Contains("Long 1h BitUnix"))
                {
                    exchange = new BitUnix(); 
                    interval = GlobalKlineInterval.OneHour;
                }
                if (str.Contains("Long 4h BitUnix"))
                {
                    exchange = new BitUnix(); 
                    interval = GlobalKlineInterval.FourHours;
                }

                if (exchange != null && interval != null)
                {
                    var result = await exchange.GetSignal((GlobalKlineInterval)interval);
                    return Ok(new { status = "Success", data = result });
                }

                await Logger.WriteLog(str);

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
