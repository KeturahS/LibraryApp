using LibraryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PayPal.Api;
using Stripe;
using System;
using System.Runtime.InteropServices;
using System.Text;

public class PaymentController : Controller
{
    

    private readonly string _clientId = "ARsfELcfmG8kO_ifIgrspG8j2Qd2XavbqlNrpHoiYQdP6--Zba27a1nsODuRTjEMv7vWbabfz5dqbQdj";
    private readonly string _clientSecret = "EKI6lWygbZ1PMtlsFL836N_vyEGxLMBQwIk4miQGBLe_Qr0HX7NkG7vbYPQVTX9Ji4ZWOHUoDlGCswzi";

    private readonly IConfiguration _configuration;
    string connectionString;
    public PaymentController(IConfiguration configuration)
    {
        _configuration = configuration;
        connectionString = configuration.GetConnectionString("myConnect");


    }
    private APIContext GetApiContext()
    {
        return new APIContext(new OAuthTokenCredential(_clientId, _clientSecret).GetAccessToken());
    }

    [HttpPost]
    public IActionResult PayWithPayPal()
    {
        try
        {
            string userName = HttpContext.Session.GetString("Current user name");
            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "You must be logged in to proceed with payment.";
                return RedirectToAction("ViewCart", "Cart");
            }

            List<(string BookTitle, string Author, string Publisher, int YearOfPublication, string ActionType)> cartItems = new List<(string, string, string, int, string)>();
            decimal totalAmount = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
            SELECT b.BookTitle, b.Author, b.Publisher, b.YearOfPublication, 
                   b.PriceForBuy, b.PriceForBorrow, c.ActionType
            FROM Cart c
            INNER JOIN Books b ON c.BookTitle = b.BookTitle AND c.Author = b.Author
            WHERE c.UserName = @UserName";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserName", userName);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string actionType = reader.GetString(6); // Borrow or Buy
                            decimal price = actionType == "buy" ? reader.GetDecimal(4) : reader.GetDecimal(5);

                            cartItems.Add((
                                reader.GetString(0),  // BookTitle
                                reader.GetString(1),  // Author
                                reader.GetString(2),  // Publisher
                                reader.GetInt32(3),   // YearOfPublication
                                actionType           // ActionType
                            ));

                            totalAmount += price;
                        }
                    }
                }
            }

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction("ViewCart", "Cart");
            }

            // שמירת פרטי העגלה ב-Session לצורך עיבוד לאחר התשלום
            HttpContext.Session.SetObject("CartItems", cartItems.Select(item => new cartItem
            {
                BookTitle = item.BookTitle,
                Author = item.Author,
                Publisher = item.Publisher,
                YearOfPublication = item.YearOfPublication,
                ActionType = item.ActionType,
                AddedDate = DateTime.Now
            }).ToList());



            var apiContext = GetApiContext();

            var payment = new Payment
            {
                intent = "sale",
                payer = new Payer { payment_method = "paypal" },
                transactions = new List<Transaction>
            {
                new Transaction
                {
                    description = "eBook Purchase",
                    amount = new Amount
                    {
                        currency = "USD",
                        total = totalAmount.ToString("F2")
                    }
                }
            },
                redirect_urls = new RedirectUrls
                {
                    return_url = Url.Action("PaymentSuccess", "Payment", null, Request.Scheme),
                    cancel_url = Url.Action("PaymentCancel", "Payment", null, Request.Scheme)
                }
            };

            var createdPayment = payment.Create(apiContext);
            var approvalUrl = createdPayment.links.FirstOrDefault(link => link.rel.Equals("approval_url", StringComparison.OrdinalIgnoreCase))?.href;

            return Redirect(approvalUrl);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error creating payment: " + ex.Message;
            return RedirectToAction("ViewCart", "Cart");
        }
    }


    public IActionResult PaymentSuccess(string paymentId, string token, string PayerID)
    {
        string userName = HttpContext.Session.GetString("Current user name");
        var cartItems = HttpContext.Session.GetObject<List<cartItem>>("CartItems");

        if (cartItems == null || !cartItems.Any())
        {
            Console.WriteLine("No items found in the cart.");
            TempData["ErrorMessage"] = "No items found in the cart.";
            return RedirectToAction("ViewCart", "Cart");
        }

        // לוג נוסף להצגת הפריטים מה-Session
        Console.WriteLine($"Retrieved {cartItems.Count} items from session:");
        foreach (var item in cartItems)
        {
            Console.WriteLine($"BookTitle: {item.BookTitle}, Author: {item.Author}, Publisher: {item.Publisher}, Year: {item.YearOfPublication}, ActionType: {item.ActionType}");
        }

        // עיבוד הפריטים והוספה לטבלאות המתאימות
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            foreach (var item in cartItems)
            {
                if (item.ActionType == "buy")
                {
                    AddToPurchasedBooks(connection, userName, item);
                }
                else if (item.ActionType == "borrow")
                {
                    AddToBorrowedBooks(connection, userName, item);
                }
            }

            // מחיקת כל הפריטים מהעגלה לאחר התשלום
            string deleteQuery = "DELETE FROM Cart WHERE UserName = @UserName";
            using (SqlCommand deleteCommand = new SqlCommand(deleteQuery, connection))
            {
                deleteCommand.Parameters.AddWithValue("@UserName", userName);
                int rowsDeleted = deleteCommand.ExecuteNonQuery();
                Console.WriteLine($"Deleted {rowsDeleted} items from the cart.");
            }
        }
        TempData["SuccessMessage"] = "Payment completed successfully!";
        return RedirectToAction("ViewCart", "Cart");
    }




    public IActionResult PaymentCancel()
    {
        TempData["ErrorMessage"] = "Payment was canceled.";
        return RedirectToAction("ViewCart", "Cart");
    }

    private void AddToPurchasedBooks(SqlConnection connection, string userName, cartItem item)
    {
        string query = @"
    INSERT INTO PurchasedBooks (UserName, BookTitle, Author, Publisher, YearOfPublication, PurchaseDate) 
    VALUES (@UserName, @BookTitle, @Author, @Publisher, @YearOfPublication, GETDATE())";

        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@UserName", userName);
            command.Parameters.AddWithValue("@BookTitle", item.BookTitle);
            command.Parameters.AddWithValue("@Author", item.Author);
            command.Parameters.AddWithValue("@Publisher", item.Publisher);
            command.Parameters.AddWithValue("@YearOfPublication", item.YearOfPublication);

            int rowsAffected = command.ExecuteNonQuery();
            Console.WriteLine($"Added to PurchasedBooks: {item.BookTitle}, Author: {item.Author}, Publisher: {item.Publisher}, Year: {item.YearOfPublication}, Rows affected: {rowsAffected}");
        }
    }

    private void AddToBorrowedBooks(SqlConnection connection, string userName, cartItem item)
    {
        string query = @"
    INSERT INTO BorrowedBooks (UserName, BookTitle, Author, Publisher, YearOfPublication, BorrowDate, ReturnDate) 
    VALUES (@UserName, @BookTitle, @Author, @Publisher, @YearOfPublication, GETDATE(), DATEADD(DAY, 30, GETDATE()))";

        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@UserName", userName);
            command.Parameters.AddWithValue("@BookTitle", item.BookTitle);
            command.Parameters.AddWithValue("@Author", item.Author);
            command.Parameters.AddWithValue("@Publisher", item.Publisher);
            command.Parameters.AddWithValue("@YearOfPublication", item.YearOfPublication);

            int rowsAffected = command.ExecuteNonQuery();
            Console.WriteLine($"Added to BorrowedBooks: {item.BookTitle}, Author: {item.Author}, Publisher: {item.Publisher}, Year: {item.YearOfPublication}, Rows affected: {rowsAffected}");
        }




    }


    [HttpPost]
    public IActionResult ProcessCreditCardPayment([FromBody] PaymentRequest request)
    {
        try
        {
            string userName = HttpContext.Session.GetString("Current user name");
            
            if (string.IsNullOrEmpty(userName))
            {
                TempData["ErrorMessage"] = "You must be logged in to proceed with payment.";
                return RedirectToAction("ViewCart", "Cart");
            }

            List<(string BookTitle, string Author, string Publisher, int YearOfPublication, string ActionType)> cartItems = new List<(string, string, string, int, string)>();
            decimal totalAmount = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = @"
                SELECT b.BookTitle, b.Author, b.Publisher, b.YearOfPublication, 
                       b.PriceForBuy, b.PriceForBorrow, c.ActionType
                FROM Cart c
                INNER JOIN Books b ON c.BookTitle = b.BookTitle AND c.Author = b.Author
                WHERE c.UserName = @UserName";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserName", userName);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string actionType = reader.GetString(6); // Borrow or Buy
                            decimal price = actionType == "buy" ? reader.GetDecimal(4) : reader.GetDecimal(5);

                            cartItems.Add((
                                reader.GetString(0),  // BookTitle
                                reader.GetString(1),  // Author
                                reader.GetString(2),  // Publisher
                                reader.GetInt32(3),   // YearOfPublication
                                actionType           // ActionType
                            ));

                            totalAmount += price;
                        }
                    }
                }
            }

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction("ViewCart", "Cart");
            }

            // שימוש במפתח הסודי של Stripe
            StripeConfiguration.ApiKey = "sk_test_51QecxbDKzSonVfg0glWsKLChtY9lccLQae8nFQXfE1iekGcbk7w4oayTR8qT9tbBEAQBzOGosH8N2bjo59FfVSQq00vaym7q2i";

            var options = new ChargeCreateOptions
            {
                Amount = (long)(totalAmount * 100), // המרת הסכום לסנטים
                Currency = "usd",
                Source = request.Token, // הטוקן שהתקבל מהלקוח
                Description = "Purchase of eBooks"
            };

            var service = new ChargeService();
            Charge charge = service.Create(options);

            if (charge.Status == "succeeded" && charge.Paid)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    foreach (var item in cartItems)
                    {
                        var cartItemObj = new cartItem
                        {
                            BookTitle = item.BookTitle,
                            Author = item.Author,
                            Publisher = item.Publisher,
                            YearOfPublication = item.YearOfPublication,
                            ActionType = item.ActionType
                        };

                        if (cartItemObj.ActionType == "buy")
                        {
                            AddToPurchasedBooks(connection, userName, cartItemObj);
                        }
                        else if (cartItemObj.ActionType == "borrow")
                        {
                            AddToBorrowedBooks(connection, userName, cartItemObj);
                        }
                    }


                    string deleteQuery = "DELETE FROM Cart WHERE UserName = @UserName";
                    using (SqlCommand deleteCommand = new SqlCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("@UserName", userName);
                        deleteCommand.ExecuteNonQuery();
                    }
                }


             

                return Json(new { success = true, message = "Payment completed successfully!"});

            }
            
            return Json(new { success = false, message = "Payment failed. Please try again." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Error processing payment: " + ex.Message });
        }
    }



    public IActionResult CreditCardPaymentPage()
    {
        string userName = HttpContext.Session.GetString("Current user name");
        if (string.IsNullOrEmpty(userName))
        {
            TempData["ErrorMessage"] = "You must be logged in to proceed.";
            return RedirectToAction("SignIn", "HomePage");
        }

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            string query = "SELECT COUNT(*) FROM Cart WHERE UserName = @UserName";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@UserName", userName);
                int itemCount = (int)command.ExecuteScalar();

                if (itemCount == 0)
                {
                    TempData["ErrorMessage"] = "Your cart is empty. Please add items before proceeding to payment.";
                    return RedirectToAction("ViewCart", "Cart");
                }
            }
        }

        return View("Payment");
    }

}




