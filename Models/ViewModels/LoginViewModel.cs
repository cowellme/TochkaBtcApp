using System.ComponentModel.DataAnnotations;

namespace TochkaBtcApp.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Введите свой username")]
        public string? UserName { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Пароль не может быть пустым")]
        public string? Password { get; set; } 
    }
}
