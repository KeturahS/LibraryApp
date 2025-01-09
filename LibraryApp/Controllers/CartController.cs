using LibraryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Security.Policy;
using static LibraryApp.Models.ConnectionToDBmodel;

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
        string userName = HttpContext.Session.GetString("CurrentUser");
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
                // בדיקה אם המשתמש השאיל או מתכנן להשאיל יותר מ-3 ספרים (Cart + BorrowedBooks)
                string totalBorrowCountQuery = @"
                     SELECT 
                    (SELECT COUNT(*) FROM BorrowedBooks WHERE UserName = @UserName AND BorrowDate >= DATEADD(DAY, -30, GETDATE()))
                            +
                    (SELECT COUNT(*) FROM Cart WHERE UserName = @UserName AND ActionType = 'borrow') AS TotalBorrowCount";

                using (SqlCommand totalBorrowCountCommand = new SqlCommand(totalBorrowCountQuery, connection))
                {
                    totalBorrowCountCommand.Parameters.AddWithValue("@UserName", userName);
                    int totalBorrowCount = (int)totalBorrowCountCommand.ExecuteScalar();

                    if (totalBorrowCount >= 3)
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
        CheckCartExpiration(); // בדיקת תוקף פריטים בעגלה
        string userName = HttpContext.Session.GetString("CurrentUser");

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
        string userName = HttpContext.Session.GetString("CurrentUser");

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


                    string query1 = "UPDATE Books SET AvailableAmountOfCopiesToBorrow = AvailableAmountOfCopiesToBorrow + 1 WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";





                    var parameters = new Dictionary<string, object>
                      {
                           { "@BookTitle", bookTitle },
                           { "@Author",  author },
                           { "@Publisher", publisher },
                           { "@YearOfPublication",  yearOfPublication },
                          };

                    // Create a connection to the database
                    ConnectionToDBModel connection1 = new ConnectionToDBModel(_configuration);

                    // Execute the query and map the results to a list of User objects


                    connection1.ExecuteNonQuery(
                     query1, parameters);

                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to remove the book. Please try again.";
                }
            }
        }

        //return RedirectToAction("UpdateAboutNewAvailableBook", "Admin", new { BookTitle = bookTitle, Author = author, Publisher = publisher, YearOfPublication = yearOfPublication });


        return RedirectToAction("ViewCart", "Cart");
    }



    public void CheckCartExpiration()
    {
        var cartItems = HttpContext.Session.GetObject<List<cartItem>>("CartItems") ?? new List<cartItem>();
        var now = DateTime.Now;
        bool itemsRemoved = false;

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            foreach (var item in cartItems.ToList()) // יצירת עותק של הרשימה כדי לעבור עליה ולבצע שינויים
            {
                if (item.ActionType == "borrow" && (now - item.AddedDate).TotalMinutes > 60)
                {
                    string query = @"
                DELETE FROM Cart 
                WHERE UserName = @UserName 
                  AND BookTitle = @BookTitle 
                  AND Author = @Author 
                  AND Publisher = @Publisher 
                  AND YearOfPublication = @YearOfPublication";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserName", HttpContext.Session.GetString("CurrentUser"));
                        command.Parameters.AddWithValue("@BookTitle", item.BookTitle);
                        command.Parameters.AddWithValue("@Author", item.Author);
                        command.Parameters.AddWithValue("@Publisher", item.Publisher);
                        command.Parameters.AddWithValue("@YearOfPublication", item.YearOfPublication);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            itemsRemoved = true;
                            Console.WriteLine($"Removed expired item from cart: {item.BookTitle}");

                            // עדכון זמינות הספר בהשאלה
                            string updateQuery = @"
                        UPDATE Books 
                        SET AvailableAmountOfCopiesToBorrow = AvailableAmountOfCopiesToBorrow + 1 
                        WHERE BookTitle = @BookTitle 
                          AND Author = @Author 
                          AND Publisher = @Publisher 
                          AND YearOfPublication = @YearOfPublication";

                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@BookTitle", item.BookTitle);
                                updateCommand.Parameters.AddWithValue("@Author", item.Author);
                                updateCommand.Parameters.AddWithValue("@Publisher", item.Publisher);
                                updateCommand.Parameters.AddWithValue("@YearOfPublication", item.YearOfPublication);
                                updateCommand.ExecuteNonQuery();
                            }

                            // הסרת הפריט מהרשימה ב-Session
                            cartItems.Remove(item);
                        }
                    }
                }
            }
        }

        // אם הוסרו פריטים, עדכון ה-Session והצגת הודעה למשתמש
        if (itemsRemoved)
        {
            HttpContext.Session.SetObject("CartItems", cartItems);
            TempData["ErrorMessage"] = "Some borrowed books were removed from your cart because they were not purchased within an hour.";
        }
    }




    [HttpPost]
    public IActionResult ChangeActionType(string bookTitle, string author, string publisher, int yearOfPublication, string newActionType)
    {

        string userName = HttpContext.Session.GetString("CurrentUser");

        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to change the action type.";
            return RedirectToAction("ViewCart", "Cart");
        }

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            // בדיקה אם המשתמש מבקש לשנות ל-"borrow"
            if (newActionType == "borrow")
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
                        return RedirectToAction("ViewCart", "Cart");
                    }
                }
            }

            // שליפת המחיר המתאים לפי סוג הפעולה
            string priceQuery = newActionType == "buy" ?
                "SELECT PriceForBuy FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication" :
                "SELECT PriceForBorrow FROM Books WHERE BookTitle = @BookTitle AND Author = @Author AND Publisher = @Publisher AND YearOfPublication = @YearOfPublication";

            
            using (SqlCommand priceCommand = new SqlCommand(priceQuery, connection))
            {
                priceCommand.Parameters.AddWithValue("@BookTitle", bookTitle);
                priceCommand.Parameters.AddWithValue("@Author", author);
                priceCommand.Parameters.AddWithValue("@Publisher", publisher);
                priceCommand.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                
            }

            // עדכון סוג הפעולה והמחיר בעגלה
            string updateQuery = @"
        UPDATE Cart 
        SET ActionType = @NewActionType, AddedDate=GETDATE()
        WHERE UserName = @UserName 
          AND BookTitle = @BookTitle 
          AND Author = @Author 
          AND Publisher = @Publisher 
          AND YearOfPublication = @YearOfPublication";

            using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
            {
                updateCommand.Parameters.AddWithValue("@NewActionType", newActionType);
                
                updateCommand.Parameters.AddWithValue("@UserName", userName);
                updateCommand.Parameters.AddWithValue("@BookTitle", bookTitle);
                updateCommand.Parameters.AddWithValue("@Author", author);
                updateCommand.Parameters.AddWithValue("@Publisher", publisher);
                updateCommand.Parameters.AddWithValue("@YearOfPublication", yearOfPublication);

                int rowsAffected = updateCommand.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "Action type and price updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update the action type. Please try again.";
                }

                // עדכון רשימת העגלה ב-Session
                var cartItems = HttpContext.Session.GetObject<List<cartItem>>("CartItems") ?? new List<cartItem>();

                // מציאת הפריט הספציפי בעגלה לעדכון
                var itemToUpdate = cartItems.FirstOrDefault(item =>
                    item.BookTitle == bookTitle &&
                    item.Author == author &&
                    item.Publisher == publisher &&
                    item.YearOfPublication == yearOfPublication);

                if (itemToUpdate != null)
                {
                    itemToUpdate.ActionType = newActionType;
                    itemToUpdate.AddedDate = DateTime.Now; // עדכון זמן ההוספה
                }

                // שמירת הרשימה המעודכנת ב-Session
                HttpContext.Session.SetObject("CartItems", cartItems);

            }
        }

        return RedirectToAction("ViewCart", "Cart");
    }







}



