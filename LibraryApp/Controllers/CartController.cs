using LibraryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Policy;

public class CartController : Controller
{
    private readonly IConfiguration _configuration;
    string connectionString;
    public CartController(IConfiguration configuration)
    {
        _configuration = configuration;
        connectionString = configuration.GetConnectionString("myConnect");
    }




    [HttpPost]
    public IActionResult AddToCart(string bookTitle, string author, string publisher, int yearOfPublication, string actionType)
    {
        // בדיקת פרטי המשתמש המחובר
        string userName = HttpContext.Session.GetString("Current user name");
        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to add books to the cart.";
            return RedirectToAction("BookDetails", "HomePage", new { bookTitle, author, publisher, yearOfPublication });
        }

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // בדיקת הפעולה: השאלה או קנייה
            if (actionType == "borrow")
            {
                // בדיקה אם המשתמש השאיל כבר 3 ספרים ב-30 הימים האחרונים
                string borrowCountQuery = "SELECT COUNT(*) FROM BorrowedBooks WHERE UserName = @UserName AND BorrowDate >= DATEADD(DAY, -30, GETDATE())";
                using (SqlCommand borrowCountCommand = new SqlCommand(borrowCountQuery, connection))
                {
                    borrowCountCommand.Parameters.AddWithValue("@UserName", userName);
                    int borrowCount = (int)borrowCountCommand.ExecuteScalar();

                    if (borrowCount >= 3)
                    {
                        TempData["ErrorMessage"] = "You cannot borrow more than 3 books within a 30-day period.";
                        return RedirectToAction("BookDetails", "HomePage", new { bookTitle, author, publisher, yearOfPublication });
                    }
                }

                // הוספת הספר לרשימת ההשאלות בעגלה
                string borrowQuery = @"
                INSERT INTO Cart (UserName, BookTitle, Author, Publisher, YearOfPublication, ActionType) 
                VALUES (@UserName, @BookTitle, @Author, @Publisher, @YearOfPublication, 'borrow');

                UPDATE Books SET AvailableAmountOfCopiesToBorrow = AvailableAmountOfCopiesToBorrow - 1
                WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication AND AvailableAmountOfCopiesToBorrow > 0";

                using (SqlCommand borrowCommand = new SqlCommand(borrowQuery, connection))
                {
                    borrowCommand.Parameters.AddWithValue("@UserName", userName);
                    borrowCommand.Parameters.AddWithValue("@BookTitle", bookTitle);
                    borrowCommand.Parameters.AddWithValue("@Author", author);
                    borrowCommand.Parameters.AddWithValue("@Publisher", publisher);
                    borrowCommand.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                    int rowsAffected = borrowCommand.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        TempData["SuccessMessage"] = "Book borrowed successfully.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to borrow the book. It might be unavailable.";
                    }

                    // לוג לאחר הוספה לעגלה
                    Console.WriteLine($"Added to cart: {bookTitle}, {author}, {publisher}, {yearOfPublication}, ActionType: {actionType}");
                }
            }
            else if (actionType == "buy")
            {
                // הוספת הספר לעגלה עבור קנייה
                string cartQuery = "INSERT INTO Cart (UserName, BookTitle, Author, Publisher, YearOfPublication, ActionType) VALUES (@UserName, @BookTitle, @Author, @Publisher, @YearOfPublication, 'buy')";

                using (SqlCommand cartCommand = new SqlCommand(cartQuery, connection))
                {
                    cartCommand.Parameters.AddWithValue("@UserName", userName);
                    cartCommand.Parameters.AddWithValue("@BookTitle", bookTitle);
                    cartCommand.Parameters.AddWithValue("@Author", author);
                    cartCommand.Parameters.AddWithValue("@Publisher", publisher);
                    cartCommand.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                    int rowsAffected = cartCommand.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        TempData["SuccessMessage"] = "Book added to cart successfully.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to add the book to the cart.";
                    }

                    // לוג לאחר הוספה לעגלה
                    Console.WriteLine($"Added to cart: {bookTitle}, {author}, {publisher}, {yearOfPublication}, ActionType: {actionType}");
                }
            }
        }

        // יצירת אובייקט cartItem חדש
        var newItem = new cartItem
        {
            BookTitle = bookTitle,
            Author = author,
            Publisher = publisher,
            YearOfPublication = yearOfPublication,
            ActionType = actionType,
            AddedDate = DateTime.Now
        };

        // הוספת הפריט לרשימת העגלה
        var cartItems = HttpContext.Session.GetObject<List<cartItem>>("CartItems") ?? new List<cartItem>();
        cartItems.Add(newItem);

        // שמירת הרשימה המעודכנת ב-Session
        HttpContext.Session.SetObject("CartItems", cartItems);

        // לוג נוסף לאחר ההוספה
        Console.WriteLine($"Cart items count after addition: {cartItems.Count}");
        foreach (var item in cartItems)
        {
            Console.WriteLine($"Added item: {item.BookTitle}, {item.Author}, {item.Publisher}, {item.YearOfPublication}, ActionType: {item.ActionType}");
        }

        return RedirectToAction("BookDetails", "HomePage", new { bookTitle, author, publisher, yearOfPublication });
    }





    [HttpGet]
    public IActionResult ViewCart()
    {
        string userName = HttpContext.Session.GetString("Current user name");

        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to view your cart.";
            return RedirectToAction("SignIn", "HomePage");
        }

        List<cartItem> cartItems = new List<cartItem>();

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            string query = @"
            SELECT c.BookTitle, c.Author, c.Publisher, c.YearOfPublication, c.ActionType, c.AddedDate, 
                   b.PriceForBuy, b.PriceForBorrow
            FROM Cart c
            INNER JOIN Books b ON c.BookTitle = b.BookTitle 
                               AND c.Author = b.Author 
                               AND c.Publisher = b.Publisher 
                               AND c.YearOfPublication = b.YearOfPublication
            WHERE c.UserName = @UserName";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        cartItems.Add(new cartItem
                        {
                            BookTitle = reader.GetString(0),
                            Author = reader.GetString(1),
                            Publisher = reader.GetString(2),
                            YearOfPublication = reader.GetInt32(3),
                            ActionType = reader.GetString(4),
                            AddedDate = reader.GetDateTime(5),
                            PriceForBuy = reader.GetDecimal(6),
                            PriceForBorrow = reader.GetDecimal(7)
                        });
                    }
                }
            }
        }

        return View("Cart",cartItems);
    }


    [HttpPost]
    public IActionResult RemoveFromCart(string bookTitle, string author, string publisher, int yearOfPublication)
    {
        string userName = HttpContext.Session.GetString("Current user name");

        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to remove books from the cart.";
            return RedirectToAction("SignIn", "HomePage");
        }

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            string query = @"
            DELETE FROM Cart 
            WHERE UserName = @UserName 
              AND BookTitle = @BookTitle 
              AND Author = @Author 
              AND Publisher = @Publisher 
              AND YearOfPublication = @YearOfPublication";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@BookTitle", bookTitle);
                command.Parameters.AddWithValue("@Author", author);
                command.Parameters.AddWithValue("@Publisher", publisher);
                command.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                int rowsAffected = command.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Book removed from cart successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove the book. Please try again.";
                }
            }
        }

        return RedirectToAction("ViewCart", "Cart");
    }





}



