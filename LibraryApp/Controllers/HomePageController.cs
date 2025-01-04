using Microsoft.AspNetCore.Mvc;
using LibraryApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using static LibraryApp.Models.ConnectionToDBmodel;

namespace LibraryApp.Controllers
{
    public class HomePageController : Controller
    {
        private readonly IConfiguration _configuration; 
        private readonly string connectionString;

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
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string sqlQuery = "INSERT INTO Users_tbl (Status, FirstName, LastName, Email, Password) VALUES (@Status, @FirstName, @LastName, @Email, @Password)";

                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Status", user.Status == "Admin" ? "PendingAdminApproval" : "User");
                        command.Parameters.AddWithValue("@FirstName", user.FirstName);
                        command.Parameters.AddWithValue("@LastName", user.LastName);
                        command.Parameters.AddWithValue("@Email", user.email);
                        command.Parameters.AddWithValue("@Password", user.Password);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            if (user.Status == "PendingAdminApproval")
                            {
                                TempData["SuccessMessage"] = "Admin request submitted successfully!";
                            }
                            return RedirectToAction("SignIn");
                        }


						Gmail gmail = new Gmail();



                        gmail.To = user.email;
						gmail.Subject = "Welcome to ebooklibraryservice – Account Successfully Created!";
						gmail.Body = "Dear " + user.FirstName + "\r\n\r\n"+ "Thank you for signing up! We’re excited to have you join our community.get started"+"\r\n\r\n"+ "If you have any questions, feel free to reach out to us at I.K.eBookLibraryService@gmail.com "+ "\r\n\r\n" + "We’re thrilled to have you on board!"
                            + "\r\n\r\n" + "Warm regards," + "\r\n\r\n" + " The I.K eBook Library Team";

						gmail.SendEmail();
					}
                }
            }

            return View("SignUp", user);
        }

        public IActionResult SignIn()
        {
            TempData["ErrorMessage"] = null;
            return View(new LogInModel());
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
                    TempData["ErrorMessage"] = "Your admin request is still pending approval.";
                    return View("SignIn", model);
                }

                if (status == "Admin")
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);
                    HttpContext.Session.SetString("Current user name", GetUser(model.email).FirstName);
                    return RedirectToAction("ShowAdminPage", "Admin");
                }

                if (status == "User")
                {
                    HttpContext.Session.SetString("CurrentUser", model.email);
                    HttpContext.Session.SetString("Current user name", GetUser(model.email).FirstName);
                    return RedirectToAction("ShowBooks");
                }
            }

            TempData["ErrorMessage"] = "Invalid email or password.";
            return View("SignIn", model);
        }




        private User GetUser(string email)
        {
            string query = "SELECT * FROM Users_tbl WHERE email = @email";

            var parameters = new Dictionary<string, object>
                {
                    { "@email", email }      
                };

            // יצירת חיבור למסד הנתונים
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // שליפת הספר מהמסד
            var user = connection.ExecuteQuery<User>(
                query, parameters, reader => new User
                {
                   FirstName = reader.GetString(1),
                   LastName = reader.GetString(2),
                }
            );


            return user.FirstOrDefault();
        }



        public IActionResult ShowBooks(string searchQuery)
        {
            List<Book> books = new List<Book>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = string.IsNullOrEmpty(searchQuery)
                    ? "SELECT * FROM Books"
                    : "SELECT * FROM Books WHERE LOWER(BookTitle) LIKE @SearchQuery";

                using (var command = new SqlCommand(query, connection))
                {
                    if (!string.IsNullOrEmpty(searchQuery))
                    {
                        command.Parameters.AddWithValue("@SearchQuery", $"%{searchQuery.ToLower()}%");
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {
                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),
                               
                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)

                            });
                        }
                    }
                }
            }

            // טוען את רשימות הסופרים והז'אנרים
            ViewBag.AllAuthors = GetAllAuthorsFromDatabase();
            ViewBag.AllGenres = GetAllGenresFromDatabase();

            ViewBag.UserName = HttpContext.Session.GetString("Current user name");
           
            ViewBag.SearchQuery = searchQuery;

            

            return View("UserPageUpdated", books);
        }

        private string IsUserValid(string email, string password)
        {
            string query = "SELECT Status FROM Users_tbl WHERE email = @Email AND Password = @Password";

            using (var connection = new SqlConnection(connectionString))
            {
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Email", email);
                    command.Parameters.AddWithValue("@Password", password);

                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader["Status"].ToString();
                        }
                    }
                }
            }

            return "NonExistent";
        }

        public IActionResult BookDetails(string bookTitle, string author, string publisher, int yearOfPublication)
        {
            Book book = null;
            List<Feedback> feedbacks = new List<Feedback>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // שאילתה לשליפת פרטי הספר
                string bookQuery = @"
        SELECT * 
        FROM Books 
        WHERE BookTitle = @BookTitle 
          AND Author = @Author 
          AND Publisher = @Publisher 
          AND YearOfPublication = @YearOfPublication";

                using (var command = new SqlCommand(bookQuery, connection))
                {
                    command.Parameters.AddWithValue("@BookTitle", bookTitle);
                    command.Parameters.AddWithValue("@Author", author);
                    command.Parameters.AddWithValue("@Publisher", publisher);
                    command.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            book = new Book
                            {
                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),
                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)
                            };
                        }
                    }
                }

                if (book == null)
                {
                    return NotFound(); // אם הספר לא נמצא
                }

                // שאילתה לשליפת הפידבקים לספר
                string feedbackQuery = @"
        SELECT UserName, Rating, Feedback AS Comment, FeedbackDate 
        FROM BookFeedback 
        WHERE BookTitle = @BookTitle 
          AND Author = @Author 
          AND Publisher = @Publisher 
          AND YearOfPublication = @YearOfPublication";

                using (var feedbackCommand = new SqlCommand(feedbackQuery, connection))
                {
                    feedbackCommand.Parameters.AddWithValue("@BookTitle", bookTitle);
                    feedbackCommand.Parameters.AddWithValue("@Author", author);
                    feedbackCommand.Parameters.AddWithValue("@Publisher", publisher);
                    feedbackCommand.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                    using (var feedbackReader = feedbackCommand.ExecuteReader())
                    {
                        while (feedbackReader.Read())
                        {
                            feedbacks.Add(new Feedback
                            {
                                UserName = feedbackReader.GetString(0),
                                Rating = feedbackReader.GetInt32(1),
                                Comment = feedbackReader.GetString(2),
                                FeedbackDate = feedbackReader.GetDateTime(3)
                            });
                        }
                    }
                }
            }

            // העברת הספר והפידבקים ל-View
            ViewBag.Feedbacks = feedbacks;
            return View(book);
        }


        [HttpGet]
        public IActionResult SearchBooks(string searchQuery)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
            SELECT * 
            FROM Books 
            WHERE 
                LOWER(BookTitle) LIKE LOWER(@SearchQuery) OR 
                LOWER(Author) LIKE LOWER(@SearchQuery) OR 
                LOWER(Publisher) LIKE LOWER(@SearchQuery)";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SearchQuery", $"%{searchQuery}%");

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {

                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),

                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)

							});
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("CurrentUser");

            ViewBag.SearchQuery = searchQuery;

            return View("UserPageUpdated", books);
        }



        public IActionResult SortBooks(string sortOption)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books";

                // הוספת מיון בהתאם לאופציה שנבחרה
                switch (sortOption)
                {
                    case "priceLowToHigh":
                        query += " ORDER BY PriceForBuy ASC";
                        break;
                    case "priceHighToLow":
                        query += " ORDER BY PriceForBuy DESC";
                        break;
                    case "popularity":
                        query += " ORDER BY Popularity DESC";
                        break;
                    case "genre":
                        query += " ORDER BY Genre ASC";
                        break;
                    case "year":
                        query += " ORDER BY YearOfPublication DESC";
                        break;
                }

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            books.Add(new Book
                            {

                                BookTitle = reader.GetString(0),
                                Author = reader.GetString(1),
                                Publisher = reader.GetString(2),
                                YearOfPublication = reader.GetInt32(3),
                                Genre = reader.GetString(4),
                                DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                PriceForBorrow = reader.GetDecimal(7),
                                PriceForBuy = reader.GetDecimal(8),
                                AgeRestriction = reader.GetString(9),
                                IsOnSale = reader.GetBoolean(10),

                                PDF = reader.GetBoolean(14),
                                epub = reader.GetBoolean(15),
                                f2b = reader.GetBoolean(16),
                                mobi = reader.GetBoolean(17),
                                Popularity = reader.GetInt32(18),
                                ImageUrl = reader.GetString(19),
                                AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)
                            });
                        }
                    }
                }
            }

            ViewBag.UserName = HttpContext.Session.GetString("CurrentUser");
            ViewBag.SortOption = sortOption;

            return View("UserPageUpdated", books);
        }



        public IActionResult FilterBooks(List<string> authors, List<string> genres, decimal? minPrice, decimal? maxPrice, string onSale, string availability, List<string> method)
        {
            List<Book> books = new List<Book>();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books WHERE 1=1";

                // סינון לפי סופרים
                if (authors != null && authors.Count > 0)
                {
                    query += " AND Author IN (" + string.Join(",", authors.Select(a => $"'{a}'")) + ")";
                }

                // סינון לפי ז'אנרים
                if (genres != null && genres.Count > 0)
                {
                    query += " AND Genre IN (" + string.Join(",", genres.Select(g => $"'{g}'")) + ")";
                }

                // סינון לפי מחיר
                if (minPrice.HasValue)
                {
                    query += " AND PriceForBuy >= @MinPrice";
                }
                if (maxPrice.HasValue)
                {
                    query += " AND PriceForBuy <= @MaxPrice";
                }

                // סינון לפי הנחה
                if (!string.IsNullOrEmpty(onSale) && onSale == "true")
                {
                    query += " AND (DISCOUNTEDPriceForBuy > 0 OR DISCOUNTEDPriceForBorrow > 0)";
                }



                // סינון לפי שיטה (קנייה או השאלה)
                if (method != null && method.Any(m => !string.IsNullOrWhiteSpace(m)))
                {

                    List<string> methodConditions = new List<string>();

                    if (method.Contains("buy"))
                    {
                        methodConditions.Add("PriceForBuy > 0");
                    }
                    if (method.Contains("borrow"))
                    {
                        methodConditions.Add("PriceForBorrow > 0 AND AvailableAmountOfCopiesToBorrow > 0");
                    }

                    if (methodConditions.Count > 0)
                    {
                        query += " AND (" + string.Join(" OR ", methodConditions) + ")";
                    }
                }
                




                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (minPrice.HasValue)
                    {
                        command.Parameters.AddWithValue("@MinPrice", minPrice.Value);
                    }
                    if (maxPrice.HasValue)
                    {
                        command.Parameters.AddWithValue("@MaxPrice", maxPrice.Value);
                    }
                    if (!string.IsNullOrEmpty(onSale))
                    {
                        command.Parameters.AddWithValue("@OnSale", onSale.ToLower() == "true");
                    }

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        
                            while (reader.Read())
                            {
                                books.Add(new Book
                                {
                                    BookTitle = reader.GetString(0),
                                    Author = reader.GetString(1),
                                    Publisher = reader.GetString(2),
                                    YearOfPublication = reader.GetInt32(3),
                                    Genre = reader.GetString(4),
                                    DISCOUNTEDPriceForBorrow = reader.GetDecimal(5),
                                    DISCOUNTEDPriceForBuy = reader.GetDecimal(6),
                                    PriceForBorrow = reader.GetDecimal(7),
                                    PriceForBuy = reader.GetDecimal(8),
                                    AgeRestriction = reader.GetString(9),
                                    IsOnSale = reader.IsDBNull(10) ? false : reader.GetBoolean(10),
                                    PDF = reader.IsDBNull(14) ? false : reader.GetBoolean(14),
                                    epub = reader.IsDBNull(15) ? false : reader.GetBoolean(15),
                                    f2b = reader.IsDBNull(16) ? false : reader.GetBoolean(16),
                                    mobi = reader.IsDBNull(17) ? false : reader.GetBoolean(17),

                                    Popularity = reader.GetInt32(18),
                                    ImageUrl = reader.GetString(19),
                                    AvailableAmountOfCopiesToBorrow = reader.GetInt32(20)
                                });
                            }
                        
                       

                    }
                }
            }

            // טוען מחדש את כל הסופרים והז'אנרים
            ViewBag.AllAuthors = GetAllAuthorsFromDatabase();
            ViewBag.AllGenres = GetAllGenresFromDatabase();

            // מעביר את ערכי הסינון ל-ViewBag כדי לשמר את הבחירות של המשתמש
            ViewBag.Authors = authors;
            ViewBag.Genres = genres;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.OnSale = onSale;
            ViewBag.Availability = availability;
            ViewBag.Method = method;

            return View("UserPageUpdated", books);
        }


        private List<string> GetAllAuthorsFromDatabase()
        {
            List<string> authors = new List<string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT DISTINCT Author FROM Books";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            authors.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return authors;
        }

        private List<string> GetAllGenresFromDatabase()
        {
            List<string> genres = new List<string>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT DISTINCT Genre FROM Books";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            genres.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return genres;
        }



        [HttpPost]
        public IActionResult AddServiceFeedback(int rating, string feedback)
        {
            string userName = HttpContext.Session.GetString("Current user name");

            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "You must be logged in to leave feedback.";
                return RedirectToAction("SignIn", "HomePage");
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = @"
            INSERT INTO ServiceFeedback (UserName, Rating, Feedback, FeedbackDate) 
            VALUES (@UserName, @Rating, @Feedback, GETDATE())";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserName", userName);
                    command.Parameters.AddWithValue("@Rating", rating);
                    command.Parameters.AddWithValue("@Feedback", feedback ?? (object)DBNull.Value);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        TempData["SuccessMessage"] = "Thank you for your feedback!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to submit feedback. Please try again.";
                    }
                }
            }

            return RedirectToAction("ShowBooks", "HomePage");
        }



        public List<(string UserName, int Rating, string Feedback, DateTime FeedbackDate)> GetServiceFeedbacks()
        {
            List<(string, int, string, DateTime)> feedbacks = new List<(string, int, string, DateTime)>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT UserName, Rating, Feedback, FeedbackDate FROM ServiceFeedback ORDER BY FeedbackDate DESC";

                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        feedbacks.Add((
                            reader.GetString(0),     // UserName
                            reader.GetInt32(1),      // Rating
                            reader.IsDBNull(2) ? "" : reader.GetString(2),  // Feedback
                            reader.GetDateTime(3)    // FeedbackDate
                        ));
                    }
                }
            }

            return feedbacks;
        }


        // פעולה שתציג את טופס הפידבק והרייטינג
        public IActionResult RateUs()
        {
            return View("RateUs"); // יצירת View בשם "RateUs"
        }


        public IActionResult feedbacksAboutUs()
        {

            // שליפת הפידבקים מבסיס הנתונים
            var feedbacks = GetServiceFeedbacks();

            // העברת הפידבקים ל-ViewBag כדי להציג אותם ב-View
            ViewBag.ServiceFeedbacks = feedbacks;

            return View("feedbacksAboutUs"); 
        }




    }
}
