using System.ComponentModel.DataAnnotations;

namespace LibraryApp.Models
{
    public class ResetPasswordModel
    {
        public string Token { get; set; }

        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\W).{8,}$", ErrorMessage = "Password must be at least 8 characters long, with at least one uppercase letter, one lowercase letter, and one special character")]

        public string Password { get; set; }
    }
}
