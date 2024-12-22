using System.ComponentModel.DataAnnotations;

namespace LibraryApp.Models
{
	public class User
	{
		[Required]
		[StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 letteres")]
		public string FirstName { get; set; }

		[Required]
		public string LastName { get; set; }

		[Required]
		[RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email format")]
		public string email { get; set; }


		[Required]
		[RegularExpression("^[0-9]{9}$", ErrorMessage = "ID must be exactly 9 digits")]

		public string ID { get; set; }

		[Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\W).{8,}$", ErrorMessage = "Password must be at least 8 characters long, with at least one uppercase letter, one lowercase letter, and one special character")]
        public string Password { get; set; }




	}
}
