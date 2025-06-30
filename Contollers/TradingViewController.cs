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
        public IActionResult Signal([FromBody] dynamic alert)
        {
            try
            {
                //16.05.2025 6:49:02: Data: {"text": "Buy"}
                string str = $"Data: {alert}";
                System.IO.File.AppendAllText("log.tmp", $"{DateTime.Now}: {str}\n");

                //if (str.Contains("Long 5m Binance"))
                //{
                //    var intrval = GlobalKlineInterval.FiveMinutes;
                //    var exch = new Models.Exc.Binance();
                //    exch.GetSignal(intrval);

                //    return Ok(new { status = "Success", data = alert });
                //}

                //if (str.Contains("Long 15m Binance"))
                //{
                //    var intrval = GlobalKlineInterval.FifteenMinutes;
                //    var exch = new Models.Exc.Binance();
                //    exch.GetSignal(intrval);

                //    return Ok(new { status = "Success", data = alert });
                //}

                if (str.Contains("Long 5m BingX"))
                {
                    var intrval = GlobalKlineInterval.FiveMinutes;
                    var exch = new Models.Exc.BingX();
                    exch.GetSignal(intrval);

                    return Ok(new { status = "Success", data = alert });
                }

                if (str.Contains("Long 15m BingX"))
                {
                    var intrval = GlobalKlineInterval.FifteenMinutes;
                    var exch = new Models.Exc.BingX();
                    exch.GetSignal(intrval);

                    return Ok(new { status = "Success", data = alert });
                }

                if (str.Contains("Long 1h BingX"))
                {
                    var intrval = GlobalKlineInterval.OneHour;
                    var exch = new Models.Exc.BingX();
                    exch.GetSignal(intrval);

                    return Ok(new { status = "Success", data = alert });
                }

                if (str.Contains("Long 4h BingX"))
                {
                    var intrval = GlobalKlineInterval.FourHours;
                    var exch = new Models.Exc.BingX();
                    exch.GetSignal(intrval);

                    return Ok(new { status = "Success", data = alert });
                }

                return Ok(new { status = "Success", data = alert });
            }
            catch (Exception e)
            {
                Error.Log(e);
                return Ok();
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
