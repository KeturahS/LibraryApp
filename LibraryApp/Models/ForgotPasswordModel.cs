using System.ComponentModel.DataAnnotations;

namespace LibraryApp.Models
{
    public class ForgotPasswordModel
    {
        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
        public string Email { get; set; }
    }
}
