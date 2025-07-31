using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using TochkaBtcApp.App.Logger;
using TochkaBtcApp.Bitunix;
using TochkaBtcApp.Models;
using TochkaBtcApp.Models.Exc;
using BingX = TochkaBtcApp.Models.Exc.BingX;

namespace TochkaBtcApp.Contollers
{
    [ApiController]
    [Route("[controller]")]
    public class hController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] object data)
        {
            try
            {
                var dataStr = data.ToString() ?? "none";
                var signalh = JsonConvert.DeserializeObject<SignalH>(dataStr);
                await Logger.WriteLog(signalh.code);
                
                var body = Crypto.Decrypt(signalh.code);

                var signal = hSignal.Parse(body);

                await using var db = new ApplicationContext();
                var user = db.Users.FirstOrDefault(x => x.Name == signal.Config.Name);
                
                if (user == null) return BadRequest();
                var signalLoc = user.GetSignals().Result?.FirstOrDefault(x => x.Data == dataStr);

                if (signalLoc == null) return BadRequest();
                if (!signalLoc.IsActive) return Ok();
                if (signalLoc.SingleWork)
                {
                    signalLoc.IsActive = false;
                    await user.UpdateSignal(signalLoc);
                }

                if (signal.Exchange.ToLower() == "bitunix")
                {
                    var exc = new BitUnix();
                    var result = await exc.BuyHSignal(user, signal);
                    return Ok(result);
                }

                if (signal.Exchange.ToLower() == "bingx")
                {
                    var exc = new Models.Exc.BingX();
                    var result = await exc.BuyHSignal(user, signal);
                    return Ok(result);
                }

                return Ok();
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }
    }

    public class SignalH
    {
        public string code { get; set; }
    }

    public class hSignal
    {
        public string Exchange { get; set; }
        public string TimeFrame { get; set; }
        public string Symbol { get; set; }
        public Config Config { get; set; }
        public string Side { get; set; }

        public string ToJsonText(Formatting settings = Formatting.None)
        {
            var json = JsonConvert.SerializeObject(this, settings);
            return json;
        }

        public string ToHash()
        {
            return Crypto.Encrypt(ToJsonText());
        }

        public static hSignal Parse(string al)
        {
            var response = JsonConvert.DeserializeObject<hSignal>(al) ?? null;
            return response;
        }
    }
}
