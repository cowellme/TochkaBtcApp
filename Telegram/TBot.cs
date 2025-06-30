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
        private static List<AppUser> _users = new List<AppUser>();
        private static ITelegramBotClient _bot;
        static public void Start(string token)
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { },
            };
            _bot = new TelegramBotClient(token);
            _bot.StartReceiving(
                (botClient, update, cancellationToken1) => HandleUpdateAsync(botClient, update, cancellationToken1),
                HandleErrorAsync, receiverOptions, cancellationToken);
            _users = ApplicationContext.GetUsers();
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception ex, HandleErrorSource errorSource, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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
                    if (update.Type == UpdateType.Message) ParseMassege(update);
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static void ParseMassege(Update update)
        {
            var id = update.Message?.Chat.Id;
            var msg = update.Message?.Text;
            var keyboard = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "Инструкция", "Я уже знаю" } }) { ResizeKeyboard = true };
            if (msg == null || id == null) return;

            if (msg.ToLower() == "/clear")
            {
                ApplicationContext.ClearError();
                _bot.SendMessage(id, "Успех");
            }

            if (msg.ToLower() == "/start")
            {
                var buttons = GetButtons(msg);
                _bot.SendMessage(id, "Привет!", replyMarkup: buttons);
            }

            if (msg.ToLower() == "баланс")
            {
                var user = _users.FirstOrDefault(x => x.TelegramId == id);
                if (user != null)
                {
                    decimal balance = Models.Exc.BingX.GetBalance(user.ApiBingx, user.SecretBingx);
                    
                    if (balance > 0)
                    {
                        _bot.SendMessage(id, $"Баланс (Futures): {balance:0.00}");
                    }
                }
            }
        }

        public static void LogError(Exception e, AppUser? appUser = null)
        {
            if (appUser != null)
            {

                _bot.SendMessage(appUser.TelegramId, $"{e.Message}");
                return;
            }

            _bot.SendMessage(6375432333, $"{e.Message}");
        }

        private static ReplyKeyboardMarkup GetButtons(string keyboard)
        {
            var buttons = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "Назад" } }) { ResizeKeyboard = true };

            switch (keyboard)
            {
                case "/start":
                    buttons = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "Баланс" } }) { ResizeKeyboard = true };
                    return buttons;

                default: return buttons;
            }
        }
    }
}
