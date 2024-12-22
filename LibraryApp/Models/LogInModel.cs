using System.ComponentModel.DataAnnotations;

namespace LibraryApp.Models
{
	public class LogInModel
	{

		[Required]
		[RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
		public string email { get; set; }


		[Required]

		public string Password { get; set; }
	}
}
