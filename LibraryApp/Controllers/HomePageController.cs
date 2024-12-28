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
                        else
                            user.Status = "User";
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
                            return View("HomePage", user);
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

                if (status == "PendingAdminApproval")
                {
                    TempData["ErrorMessage"] = "Your request to become an admin is still pending approval. You will receive an update via email once it's reviewed.";
                    return View("SignIn", model);
                }

                if (status == "Admin")
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);
                    return View("Admin_Home_Page", model);
                }

                if (status == "User")
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);
                    return RedirectToAction("ShowBooks");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                }
            }

            TempData["ErrorMessage"] = null;
            return View("SignIn", model);
        }


        public IActionResult ShowBooks(string searchQuery)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query;

                if (string.IsNullOrEmpty(searchQuery))
                {
                    query = "SELECT * FROM Books"; // שלוף את כל הספרים אם אין חיפוש
                }
                else
                {
                    // הפוך את השאילתה ללא תלויה באותיות קטנות/גדולות
                    query = "SELECT * FROM Books WHERE LOWER(Title) LIKE LOWER(@SearchQuery)";
                }

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        command.Parameters.AddWithValue("@SearchQuery", "%" + searchQuery.ToLower() + "%");
                    }

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {
                                Id = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                Author = reader.GetString(2),
                                Publisher = reader.GetString(3),
                                BorrowPrice = reader.GetDecimal(4),
                                BuyPrice = reader.GetDecimal(5),
                                AvailableCopies = reader.GetInt32(6),
                                ImageUrl = reader.GetString(7)
                            });
                        }
                    }
                }
                connection.Close();
            }

            ViewBag.UserName = HttpContext.Session.GetString("CurrentUser");
            ViewBag.SearchQuery = searchQuery;

            return View("UserPageUpdated", books);
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


        [HttpPost]
        public IActionResult BorrowBook(int bookId)
        {
            string currentUser = HttpContext.Session.GetString("CurrentUser");

            if (string.IsNullOrEmpty(currentUser))
            {
                TempData["ErrorMessage"] = "You need to log in to borrow books.";
                return RedirectToAction("SignIn");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // בדיקה כמה ספרים כבר מושאלים על ידי המשתמש
                string countQuery = "SELECT COUNT(*) FROM BorrowedBooks WHERE UserEmail = @UserEmail";
                using (SqlCommand countCommand = new SqlCommand(countQuery, connection))
                {
                    countCommand.Parameters.AddWithValue("@UserEmail", currentUser);

                    int borrowedCount = (int)countCommand.ExecuteScalar();

                    if (borrowedCount >= 3)
                    {
                        TempData["ErrorMessage"] = "You can only borrow up to 3 books.";
                        return RedirectToAction("BookDetails", new { id = bookId });
                    }
                }

                // בדיקה אם הספר זמין
                string availabilityQuery = "SELECT AvailableCopies FROM Books WHERE Id = @BookId";
                using (SqlCommand availabilityCommand = new SqlCommand(availabilityQuery, connection))
                {
                    availabilityCommand.Parameters.AddWithValue("@BookId", bookId);
                    int availableCopies = (int)availabilityCommand.ExecuteScalar();

                    if (availableCopies <= 0)
                    {
                        TempData["ErrorMessage"] = "This book is currently not available.";
                        return RedirectToAction("BookDetails", new { id = bookId });
                    }
                }

                // הוספת הרשומה לטבלת ההשאלות
                string borrowQuery = "INSERT INTO BorrowedBooks (UserEmail, BookId, BorrowDate) VALUES (@UserEmail, @BookId, GETDATE())";
                using (SqlCommand borrowCommand = new SqlCommand(borrowQuery, connection))
                {
                    borrowCommand.Parameters.AddWithValue("@UserEmail", currentUser);
                    borrowCommand.Parameters.AddWithValue("@BookId", bookId);
                    borrowCommand.ExecuteNonQuery();
                }

                // עדכון עותקים זמינים
                string updateQuery = "UPDATE Books SET AvailableCopies = AvailableCopies - 1 WHERE Id = @BookId";
                using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                {
                    updateCommand.Parameters.AddWithValue("@BookId", bookId);
                    updateCommand.ExecuteNonQuery();
                }
            }

            TempData["SuccessMessage"] = "Book borrowed successfully!";
            return RedirectToAction("BookDetails", new { id = bookId });
        }


        [HttpPost]
        public IActionResult PurchaseBook(int bookId)
        {
            string currentUser = HttpContext.Session.GetString("CurrentUser");

            if (string.IsNullOrEmpty(currentUser))
            {
                TempData["ErrorMessage"] = "You need to log in to purchase books.";
                return RedirectToAction("SignIn");
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // הוספת רכישה לטבלה
                string purchaseQuery = "INSERT INTO PurchasedBooks (UserEmail, BookId, PurchaseDate) VALUES (@UserEmail, @BookId, GETDATE())";
                using (SqlCommand purchaseCommand = new SqlCommand(purchaseQuery, connection))
                {
                    purchaseCommand.Parameters.AddWithValue("@UserEmail", currentUser);
                    purchaseCommand.Parameters.AddWithValue("@BookId", bookId);
                    purchaseCommand.ExecuteNonQuery();
                }
            }

            TempData["SuccessMessage"] = "Book purchased successfully!";
            return RedirectToAction("BookDetails", new { id = bookId });
        }



    }
}
