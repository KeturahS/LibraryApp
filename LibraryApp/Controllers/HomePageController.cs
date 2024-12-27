using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;


using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Reflection.Metadata.Ecma335;
using System.Data;
using LibraryApp.Models.ViewModel;



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
			if (ModelState.IsValid)
			{              

                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
				{
					connection.Open();

					string sqlQuery = "INSERT INTO Users_tbl VALUES (@Value1, @Value2, @Value3, @Value4, @Value5)";

					using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(sqlQuery, connection))
					{


                        if (user.Status == "Admin")
                        {
                            // Save the admin request as "PendingApproval"
                            user.Status = "PendingAdminApproval";
                        }
                        // Set the parameter values
                        command.Parameters.AddWithValue("@Value1", user.Status);
                        command.Parameters.AddWithValue("@Value2", user.FirstName);
						command.Parameters.AddWithValue("@Value3", user.LastName);
						command.Parameters.AddWithValue("@Value4", user.email);
						command.Parameters.AddWithValue("@Value5", user.Password);

						int rowsAffected = command.ExecuteNonQuery();
						Console.WriteLine($"Rows affected: {rowsAffected}");

						if (rowsAffected > 0)
						{
                            if (user.Status == "PendingAdminApproval")
                            {
                                return View("HomePage");
                             
                            }


                            return View("SignIn", user);
						}
						else
						{
							return View("SignUp", user);
						}

					}

				}

			}
			else
				return View("SignUp", user);


		}



        


        public IActionResult SignIn()
		{
			LogInModel user= new LogInModel();

            TempData["ErrorMessage"] = null;

            return View("SignIn", user);

		}



        [HttpPost]
        public IActionResult Login(LogInModel model)
        {
            if (ModelState.IsValid)
            {
                string status = IsUserValid(model.email, model.Password);

				if (status == "NonExistent")
				{
					TempData["ErrorMessage"] = "No account exists with the provided email. Please check your email or sign up for an account.";
					return View("SignIn", model);
				}

				if (status == "StillPendingAdminApproval")
				{
					TempData["ErrorMessage"] = "Your request to become an admin is still pending approval. You will receive an update via email once it's reviewed.";
					return View("SignIn",model);
				}

                if (status == "Admin" )
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);

                    return RedirectToAction("ShowAdminPage", "Admin");
                }



                if (status=="User")
                {

                   
                    // יצירת אובייקט ספרים
                    List<Book> books = new List<Book>();

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string query = "SELECT * FROM Books"; // שינוי שם הטבלה אם יש צורך

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    books.Add(new Book
                                    {
                                        Id = reader.GetInt32(0), // Id
                                        Title = reader.GetString(1), // Title
                                        Author = reader.GetString(2), // Author
                                        Publisher = reader.GetString(3), // Publisher
                                        BorrowPrice = reader.GetDecimal(4), // BorrowPrice
                                        BuyPrice = reader.GetDecimal(5), // BuyPrice
                                        AvailableCopies = reader.GetInt32(6), // AvailableCopies
                                        ImageUrl = reader.GetString(7) // ImageUrl
                                    });
                                }
                            }
                        }
                        connection.Close();
                    }

                    // שמירת המשתמש כמשתמש מחובר
                    HttpContext.Session.SetString("CurrentUser", model.email);

                    // שליחת הספרים לתצוגה
                    return View("UserPageUpdated", books);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                }
            }

            TempData["ErrorMessage"] = null;

            return View("SignIn", model);
        }



        private string IsUserValid(string email, string password)
		{
		
			// שאילתה לבדיקת אימייל וסיסמה
			string query = "SELECT Status FROM Users_tbl WHERE email = @email AND Password = @Password ";

			using (SqlConnection connection = new SqlConnection(connectionString))
			{
				using (SqlCommand command = new SqlCommand(query, connection))
				{
					// הוספת פרמטרים כדי למנוע SQL Injection
					command.Parameters.AddWithValue("@email", email);
					command.Parameters.AddWithValue("@Password", password);


					connection.Open();

					using (SqlDataReader reader = command.ExecuteReader())
					{
						if (reader.Read())
						{
							string status = reader["Status"].ToString(); 
							if (status == "PendingAdminApproval")
								return "StillPendingAdminApproval";

                            if (status == "SuperAdmin" || status == "Admin")
                                return "Admin";

                            return "User";
						}

                       
					}
                    				    
				}
			}

			return "NonExistent";
		}




		public IActionResult UserPage()
		{

			return View();

		}


        public IActionResult BookDetails(int id)
        {
            Book book = null;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books WHERE Id = @BookId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@BookId", id);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            book = new Book
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Author = reader.GetString(2),
                                Publisher = reader.GetString(3),
                                BorrowPrice = reader.GetDecimal(4),
                                BuyPrice = reader.GetDecimal(5),
                                AvailableCopies = reader.GetInt32(6),
                                ImageUrl = reader.GetString(7)
                            };
                        }
                    }
                }
                connection.Close();
            }

            if (book == null)
            {
                return NotFound(); // אם הספר לא נמצא
            }

            return View(book);
        }

    }
}
