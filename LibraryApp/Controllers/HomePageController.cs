using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;


using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Reflection.Metadata.Ecma335;

namespace LibraryApp.Controllers
{
	public class HomePageController : Controller
	{
		private readonly IConfiguration _configuration;
		string connectionString;
		public HomePageController(IConfiguration configuration)
		{
			_configuration = configuration;
			connectionString = configuration.GetConnectionString("myConnect");


		}




		



		public IActionResult HomePage()
		{
			
			return View();
			
		}


		public IActionResult SignUp()
		{
			User newUser = new User();

			return View("SignUp", newUser);
		}


		public IActionResult SubmitUser(User user)
		{
			//if (ModelState.IsValid)
			//{

			//	return View("HomePage",user);
			//}

			//else
			//{
			//	return View("SignUp", user);
			//}



			if (ModelState.IsValid)
			{
				using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
				{
					connection.Open();

					string sqlQuery = "INSERT INTO Users_tbl VALUES (@Value1, @Value2, @Value3, @Value4)";

					using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(sqlQuery, connection))
					{
						// Set the parameter values
						command.Parameters.AddWithValue("@Value1", user.FirstName);
						command.Parameters.AddWithValue("@Value2", user.LastName);
						command.Parameters.AddWithValue("@Value3", user.email);
						command.Parameters.AddWithValue("@Value4", user.Password);

						int rowsAffected = command.ExecuteNonQuery();
						Console.WriteLine($"Rows affected: {rowsAffected}");

						if (rowsAffected > 0)
						{
							return View("HomePage", user);
						}
						else
						{
							return View("SignUp", user);
						}

					}

					connection.Close();
				}

			}
			else
				return View("SignUp", user);


		}


		public IActionResult SignIn()
		{
			LogInModel user= new LogInModel();


			return View("SignIn", user);

		}



		[HttpPost]
		public IActionResult Login(LogInModel model)
		{
			if (ModelState.IsValid)
			{
				if (IsUserValid(model.email, model.Password))
				{

                    User user = new User();

                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                    {
                        connection.Open();

                        string sqlQuery = "SELECT FirstName FROM Users_tbl WHERE email = @Email";

                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(sqlQuery, connection))
                        {

                            command.Parameters.AddWithValue("@Email", model.email);

                            SqlDataReader reader = command.ExecuteReader();

                            while (reader.Read())
                            {
                                user.FirstName = reader.GetString(0);
                               
                            }

                            reader.Close();
                        }

                        connection.Close();
                    }



                    HttpContext.Session.SetString("CurrentUser", model.email);
					var sessionValue = HttpContext.Session.GetString("CurrentUser");
					ViewBag.sessionValue=sessionValue;
					ViewBag.UserName = user.FirstName;
					// משתמש נמצא - המשך לפעולה הבאה
					return View("UserPage");


				}
				else
				{
					// משתמש לא נמצא
					ModelState.AddModelError(string.Empty, "Invalid email or password.");
				}
			}

			return View("SignIn", model);
		}


		private bool IsUserValid(string email, string password)
		{
			bool isValid = false;

			// שאילתה לבדיקת אימייל וסיסמה
			string query = "SELECT COUNT(*) FROM Users_tbl WHERE email = @email AND Password = @Password";

			using (SqlConnection connection = new SqlConnection(connectionString))
			{
				using (SqlCommand command = new SqlCommand(query, connection))
				{
					// הוספת פרמטרים כדי למנוע SQL Injection
					command.Parameters.AddWithValue("@email", email);
					command.Parameters.AddWithValue("@Password", password);

					connection.Open();
					int count = (int)command.ExecuteScalar();
					isValid = count > 0;
				}
			}

			return isValid;
		}




		public IActionResult UserPage()
		{





			return View();

		}













	}
}
