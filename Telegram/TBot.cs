using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Telegram.Bot.Polling;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TochkaBtcApp.Models;

namespace TochkaBtcApp.Telegram
{
    public class TBot
    {
        private static List<AppUser>? _users;
        private static ITelegramBotClient? _bot;
        public TBot()
        {
            Start();
        }

        public void Start()
        {
            try
            {
                var cts = new CancellationTokenSource();
                var cancellationToken = cts.Token;
                var receiverOptions = new ReceiverOptions
                {
                    AllowedUpdates = { },
                };
                _bot = new TelegramBotClient("7166269500:AAHrIa_nw0dXi9AfLB2X2IPeAfsD6snjftA");
                _bot.StartReceiving(
                    (botClient, update, cancellationToken1) => HandleUpdateAsync(botClient, update, cancellationToken1),
                    HandleErrorAsync, receiverOptions, cancellationToken);
                _users = ApplicationContext.GetUsers();
            }
            catch (Exception e)
            {
                Error.Log(e);
            }
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception ex, HandleErrorSource errorSource, CancellationToken cancellationToken)
        {
            return Task.Run(() => Error.Log(ex), cancellationToken);
        }

        /// <summary>
        /// Обработка входящих сигналов (сообщения, фотки, гс и т.д.)
        /// </summary>
        /// <param name="botClient"></param>
        /// <param name="update"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (update.Type == UpdateType.Message) ParseMessage(update);
                }, cancellationToken);
            }
            catch (Exception e)
            {
                Error.Log(e);
                throw;
            }
        }

        private static async Task ParseMessage(Update update)
        {
            var id = update.Message?.Chat.Id;
            var msg = update.Message?.Text;
            if (msg == null || id == null) return;

            if (msg.ToLower() == "/clear")
            {
                await ApplicationContext.ClearError();
                await _bot.SendMessage(id, "Успех");
            }

            if (msg.ToLower() == "/start")
            {
                var buttons = GetButtons(msg);
                await _bot.SendMessage(id, "Привет!", replyMarkup: buttons);
            }

            if (msg.ToLower() == "позиции")
            {
                try
                {
                    var user = _users.FirstOrDefault(x => x.TelegramId == id);
                    if (user != null)
                    {
                        var positions = await Models.Exc.BingX.GetPositions(user.ApiBingx, user.SecretBingx);

                        if (positions != null)
                        {
                            if (positions.Count < 1)
                            {
                                var message = $"Открытых позиций нет";
                                await _bot.SendMessage(id, message);
                            }

                            foreach (var pos in positions)
                            {
                                var message = $"Символ: {pos.Symbol} {pos.Side}\n" +
                                              $"Покупка: {pos.AveragePrice:0.00}\n" +
                                              $"PNL: {pos.UnrealizedPnlRatio:0.0000}";
                                await _bot.SendMessage(id, message);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Error.Log(e);
                }
            }

            if (msg.ToLower() == "баланс")
            {
                try
                {
                    var user = _users.FirstOrDefault(x => x.TelegramId == id);
                    if (user != null)
                    {
                        decimal balance = Models.Exc.BingX.GetBalance(user.ApiBingx, user.SecretBingx);

                        if (balance > 0)
                        {
                            _bot.SendMessage(id, $"Баланс (Futures): $ {balance:0.00}");
                        }
                    }
                }
                catch (Exception e)
                {
                    Error.Log(e);
                }
            }
        }

        public static void LogError(Exception e, AppUser? appUser = null)
        {
            var message = e.Message;

            if (message.Contains("Insufficient"))
            {
                message = $@"Недостаточно маржи для открытия сделки";
            }

            if (appUser != null)
            {

                _bot.SendMessage(appUser.TelegramId, $"{message}");
                return;
            }

            _bot.SendMessage(6375432333, $"{message}");
        }

        private static ReplyKeyboardMarkup GetButtons(string keyboard)
        {
            var buttons = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "Назад" } }) { ResizeKeyboard = true };

            switch (keyboard)
            {
                case "/start":
                    buttons = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "Баланс", "Позиции" } }) { ResizeKeyboard = true };
                    return buttons;

                default: return buttons;
            }
        }
    }
}
