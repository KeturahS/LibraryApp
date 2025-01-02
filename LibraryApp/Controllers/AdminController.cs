using LibraryApp.Models;
using LibraryApp.Models.ViewModel;
using LibraryApp.Services;
using LibraryApp.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Security.Policy;
using static LibraryApp.Models.ConnectionToDBmodel;
using PaypalServerSdk.Standard.Models;

namespace LibraryApp.Controllers
{
    public class AdminController : Controller
    {

        private readonly IConfiguration _configuration;
        string connectionString;
        public AdminViewModel AdminPageModel;
        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = configuration.GetConnectionString("myConnect");
            AdminPageModel = new AdminViewModel();



        }


        public IActionResult ShowAdminPage()
        {

            SetAdminPage();


            return View("Admin_Home_Page", AdminPageModel);
        }

        public void SetAdminPage()
        {
            AdminPageModel.AdminUserRequests = new List<User>();
            AdminPageModel.AdminUserRequests = GetPendingAdminRequests();
            AdminPageModel.AllUsers = new List<User>();
            AdminPageModel.AllUsers = GetAllUsers();
            AdminPageModel.NewUser = new User();
            // AdminPageModel.AllBooks = new List<Book>();
            // AdminPageModel.AllBooks = GetAllBooks();

        }






        public List<User> GetPendingAdminRequests()
        {

            List<User> PendingAdminRequests = new List<User>();

            string query = "SELECT * FROM Users_tbl WHERE Status = 'PendingAdminApproval'"; // Simple SQL query

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open(); // Open connection
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader()) // Execute query
                    {
                        while (reader.Read()) // Read the data
                        {
                            var user = new User
                            {
                                FirstName = reader.GetString(1),
                                LastName = reader.GetString(2),
                                email = reader.GetString(3),
                                // Add other fields as necessary
                            };

                            PendingAdminRequests.Add(user);
                        }
                    }
                }
            }

            return PendingAdminRequests; // Return the list of pending users
        }


        [HttpPost]
        public IActionResult ApproveRejectDeleteAdd(string userId, string action)
        {
            if (action == "approve")
            {

                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Admin" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteNonQuery(updateQuery, parameters);

                Gmail gmail = new Gmail();

                gmail.To = userId;
                gmail.Subject = "Approved admin request";
                gmail.Body = "Dear " + userId + ", we are glad to inform you about being approved as admin, welcome to the team!";

                gmail.SendEmail();


            }

            if (action == "reject")
            {
                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Rejected" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteNonQuery(updateQuery, parameters);


            }


            if (action == "Delete")
            {
                var updateQuery = "DELETE FROM Users_tbl WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                        { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteNonQuery(updateQuery, parameters);


            }



            return RedirectToAction("ShowAdminPage");
        }






        public List<User> GetAllUsers()
        {
            // SQL query to get all users except the current one
            var updateQuery = "SELECT * FROM Users_tbl WHERE email <> @CurrentUser";

            // Set up the parameters
            var parameters = new Dictionary<string, object>
            {
                { "@CurrentUser", HttpContext.Session.GetString("CurrentUser") }
            };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects
            return connection.ExecuteQuery<User>(
                updateQuery,
                parameters,
                reader => new User
                {
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    email = reader.GetString(reader.GetOrdinal("email")),
                    Password = reader.GetString(reader.GetOrdinal("Password"))
                }
            );

        }





        //public List<Book> GetAllBooks()
        //{
        //    // SQL query to get all users except the current one
        //    var updateQuery = "SELECT * FROM Books";

        //    // Create a connection to the database
        //    ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);


        //    var parameters = new Dictionary<string, object> { };

        //    // Execute the query and map the results to a list of User objects
        //    return connection.ExecuteQuery<Book>(
        //     updateQuery, parameters, reader => new Book
        //     {
        //         // Map database columns to Book properties
        //         BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
        //         Author = reader.GetString(reader.GetOrdinal("Author")),
        //         Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
        //         YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
        //         Genre = reader.GetString(reader.GetOrdinal("Genre")),
        //         DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
        //         DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
        //         PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
        //         PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
        //         AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
        //         IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
        //         PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
        //         epub = reader.GetBoolean(reader.GetOrdinal("epub")),
        //         f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
        //         mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
        //         Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
        //         ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl"))
        //     }
        //     );

        //}




        public IActionResult SendEmptyBook()
        {
            TempData["Message"] = null;

            return View("AddBooks", new Book());
        }



        public IActionResult AddBook(Book book)
        {

            string query = "SELECT COUNT(1) FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@BookTitle", book.BookTitle },
                   { "@Author",  book.Author },
                   { "@Publisher",  book.Publisher },
                   { "@YearOfPublication",  book.YearOfPublication }
                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            var result = connection.ExecuteQuery<int>(query, parameters, reader => reader.GetInt32(0)).FirstOrDefault();

            if (result == 1)
            {
                TempData["Message"] = "Book already exists in the eBook Library";


                return View("AddBooks", book);

            }


            if (book.PriceForBorrow > book.PriceForBuy)
            {
                TempData["Message"] = "Borrowing price must be lower than purchase price";


                return View("AddBooks", book);
            }

            if (book.DISCOUNTEDPriceForBorrow > book.PriceForBorrow)
            {
                TempData["Message"] = "Discounted price for borrowing must be smaller than regular price for borrowing";


                return View("AddBooks", book);
            }

            if (book.DISCOUNTEDPriceForBorrow < 0)
            {
                TempData["Message"] = "Discounted price must positive";


                return View("AddBooks", book);

            }


            if (book.DISCOUNTEDPriceForBuy > book.PriceForBuy)
            {
                TempData["Message"] = "Discounted price for buying must be smaller than regular purchase price";


                return View("AddBooks", book);
            }

            if (book.DISCOUNTEDPriceForBuy < 0)
            {
                TempData["Message"] = "Discounted price must not be negetive";


                return View("AddBooks", book);

            }



            var updateQuery = "INSERT INTO Books VALUES (@BookTitle, @Author, @Publisher, @YearOfPublication, @Genre, @DISCOUNTEDPriceForBorrow, @DISCOUNTEDPriceForBuy, @PriceForBorrow, @PriceForBuy, @AgeRestriction, @IsOnSale, @PDF, @epub, @f2b, @mobi, @Popularity, @ImageUrl)";

            // Set up the parameters

            var parameters2 = new Dictionary<string, object>
                {
                    { "@BookTitle", book.BookTitle },
                    { "@Author",  book.Author },
                    { "@Publisher",  book.Publisher },
                    { "@YearOfPublication",  book.YearOfPublication },
                    { "@Genre",  book.Genre },
                    { "@DISCOUNTEDPriceForBorrow", book.DISCOUNTEDPriceForBorrow },
                    { "@DISCOUNTEDPriceForBuy",book.DISCOUNTEDPriceForBuy },
                    { "@PriceForBorrow", book.PriceForBorrow },
                    { "@PriceForBuy",  book.PriceForBuy },
                    { "@AgeRestriction",  book.AgeRestriction },
                    { "@IsOnSale",  book.IsOnSale },
                    { "@PDF", book.PDF },
                    { "@epub", book.epub },
                    { "@f2b", book.f2b },
                    { "@mobi", book.mobi },
                    { "@Popularity",  book.Popularity },
                    { "@ImageUrl",  book.ImageUrl }
                };


            // Execute the query to insert the book
            connection.ExecuteNonQuery(
                   updateQuery,
                   parameters2
               );

            TempData["BookAddedSuccessfully"] = "The Book" + @book.BookTitle + " you submitted has been added successfuly";



            return View("AddBooks", new Book());


        }




        public IActionResult SelectBook(string BookChosen)  /// מחזיר את כל הספרים שנבחרו שיש להם את התחילית שהוזנה בתיבה החיפוש
        {

            string query = "SELECT * FROM Books WHERE BookTitle LIKE @prefix OR Author LIKE @prefix OR Publisher LIKE @prefix";
            var parameters = new Dictionary<string, object>
            {
                { "@prefix", BookChosen + "%" }
            };


            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects

            AdminPageModel.SelectedBooks = new List<Book>();

            AdminPageModel.SelectedBooks =
            connection.ExecuteQuery<Book>(
             query, parameters, reader => new Book
             {
                 // Map database columns to Book properties
                 BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                 Author = reader.GetString(reader.GetOrdinal("Author")),
                 Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                 YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                 Genre = reader.GetString(reader.GetOrdinal("Genre")),
                 DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                 DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                 PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                 PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                 AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                 IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                 PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                 epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                 f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                 mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                 Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                 ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl"))
             }
             );


            SetAdminPage();


            return View("Admin_Home_Page", AdminPageModel);

        }



        public IActionResult ActionsForSelectedBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {

            Book book = GetBook(BookTitle, Author, Publisher, YearOfPublication);

            if (book == null)
            {
                // אם לא נמצא ספר, החזרת View מתאים או הודעת שגיאה
                return View("Error", "The requested book was not found.");
            }

            // החזרת View עם הספר
            return View(book);


        }



        private Book GetBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {
            string query = "SELECT * FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";

            var parameters = new Dictionary<string, object>
    {
        { "@BookTitle", BookTitle },
        { "@Author", Author },
        { "@Publisher", Publisher },
        { "@YearOfPublication", YearOfPublication }
    };

            // יצירת חיבור למסד הנתונים
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // שליפת הספר מהמסד
            var books = connection.ExecuteQuery<Book>(
                query, parameters, reader => new Book
                {
                    BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                    Author = reader.GetString(reader.GetOrdinal("Author")),
                    Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                    YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                    Genre = reader.GetString(reader.GetOrdinal("Genre")),
                    DISCOUNTEDPriceForBorrow = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBorrow")),
                    DISCOUNTEDPriceForBuy = reader.GetDecimal(reader.GetOrdinal("DISCOUNTEDPriceForBuy")),
                    PriceForBorrow = reader.GetDecimal(reader.GetOrdinal("PriceForBorrow")),
                    PriceForBuy = reader.GetDecimal(reader.GetOrdinal("PriceForBuy")),
                    AgeRestriction = reader.GetString(reader.GetOrdinal("AgeRestriction")),
                    IsOnSale = reader.GetBoolean(reader.GetOrdinal("IsOnSale")),
                    PDF = reader.GetBoolean(reader.GetOrdinal("PDF")),
                    epub = reader.GetBoolean(reader.GetOrdinal("epub")),
                    f2b = reader.GetBoolean(reader.GetOrdinal("f2b")),
                    mobi = reader.GetBoolean(reader.GetOrdinal("mobi")),
                    Popularity = reader.GetInt32(reader.GetOrdinal("Popularity")),
                    ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl"))
                }
            );


            return books.FirstOrDefault();
        }


        public IActionResult UpdateBook(string BookTitle, string Author, string Publisher, int YearOfPublication, decimal PriceForBorrow, decimal PriceForBuy, decimal DISCOUNTEDPriceForBorrow, decimal DISCOUNTEDPriceForBuy, bool PDF, bool epub, bool f2b, bool mobi)
        {
            Book book = GetBook(BookTitle, Author, Publisher, YearOfPublication);
            bool OnSALE = false;

            if (PriceForBorrow > PriceForBuy || DISCOUNTEDPriceForBorrow > PriceForBuy)
            {

                TempData["Message"] = "Price of borrowing must be less than price of purchasing";
                return View("ActionsForSelectedBook", book);

            }


            if (DISCOUNTEDPriceForBorrow > PriceForBorrow)
            {
                TempData["Message"] = "Discounted price for borrowing must be smaller than regular price for borrowing";
                return View("ActionsForSelectedBook", book);
            }


            if (DISCOUNTEDPriceForBuy > PriceForBuy)
            {
                TempData["Message"] = "Discounted price for buying must be smaller than regular purchase price";
                return View("ActionsForSelectedBook", book);
            }



            if (DISCOUNTEDPriceForBorrow < PriceForBorrow && DISCOUNTEDPriceForBorrow > 0)
            {
                OnSALE = true;

            }

            if (DISCOUNTEDPriceForBuy < PriceForBuy && DISCOUNTEDPriceForBuy > 0)
            {
                OnSALE = true;

            }

            if (DISCOUNTEDPriceForBorrow <= 0)
            {
                DISCOUNTEDPriceForBorrow = book.DISCOUNTEDPriceForBorrow;

                if (DISCOUNTEDPriceForBorrow < PriceForBorrow && DISCOUNTEDPriceForBorrow > 0)
                {
                    OnSALE = true;



                }

            }

            if (DISCOUNTEDPriceForBuy <= 0)
            {
                DISCOUNTEDPriceForBuy = book.DISCOUNTEDPriceForBuy;
                if (DISCOUNTEDPriceForBuy < PriceForBuy && DISCOUNTEDPriceForBuy > 0)
                {
                    OnSALE = true;

                }
            }




            string query = "UPDATE Books SET PriceForBorrow = @PriceForBorrow, PriceForBuy = @PriceForBuy, DISCOUNTEDPriceForBorrow= @DISCOUNTEDPriceForBorrow, DISCOUNTEDPriceForBuy=@DISCOUNTEDPriceForBuy, IsOnSale = @IsOnSale, PDF = @PDF, epub = @epub, f2b = @f2b, mobi= @mobi WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",  Publisher },
                   { "@YearOfPublication",  YearOfPublication },
                   { "@DISCOUNTEDPriceForBorrow", DISCOUNTEDPriceForBorrow },
                   { "@DISCOUNTEDPriceForBuy", DISCOUNTEDPriceForBuy },
                   { "@PriceForBorrow", PriceForBorrow },
                   { "@PriceForBuy",  PriceForBuy },
                   { "@IsOnSale",  OnSALE },
                   { "@PDF", PDF },
                   { "@epub", epub },
                   { "@f2b", f2b },
                   { "@mobi", mobi }
                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects



            connection.ExecuteNonQuery(
             query, parameters);


            Book book1 = GetBook(BookTitle, Author, Publisher, YearOfPublication);



            return View("ActionsForSelectedBook", book1);


        }

        public IActionResult DeleteSale(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {

            string query = "UPDATE Books SET IsOnSale = @IsOnSale, DISCOUNTEDPriceForBorrow= @DISCOUNTEDPriceForBorrow,  DISCOUNTEDPriceForBuy = @DISCOUNTEDPriceForBuy WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>
              {
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",  Publisher },
                   { "@YearOfPublication",  YearOfPublication },
                   { "@DISCOUNTEDPriceForBorrow", -1 },
                   { "@DISCOUNTEDPriceForBuy",-1 },
                   { "@IsOnSale",  false },

                };

            // Create a connection to the database
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);

            // Execute the query and map the results to a list of User objects



            connection.ExecuteNonQuery(
             query, parameters);


            Book book1 = GetBook(BookTitle, Author, Publisher, YearOfPublication);



            return View("ActionsForSelectedBook", book1);




        }


        public IActionResult DeleteBook(string BookTitle, string Author, string Publisher, int YearOfPublication)
        {


            var updateQuery = "DELETE FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher= @Publisher AND YearOfPublication= @YearOfPublication";

            var parameters = new Dictionary<string, object>{
                   { "@BookTitle", BookTitle },
                   { "@Author",  Author },
                   { "@Publisher",  Publisher },
                   { "@YearOfPublication",  YearOfPublication }};

            // Execute the update using ExecuteQuery
            ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
            connection.ExecuteNonQuery(updateQuery, parameters);

            TempData["MESSAGEdelete"] = "Book has been deleted successfuly";

            return View("ActionsForSelectedBook", new Book());

        }


        public IActionResult ShowBorrowingWaitingList(string UserId, string BookTitle, string Author, string Publisher, int YearOfPublication)

        { 

            return View("BorrowingWaitingList");

        }




    }
}
