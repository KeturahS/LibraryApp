using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PayPal.Api;
using System;

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
            // שליפת עגלת הקניות
            List<int> cart = HttpContext.Session.GetObject<List<int>>("Cart") ?? new List<int>();

            decimal totalAmount = 0;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT * FROM Books WHERE Id IN (" + string.Join(",", cart) + ")";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            totalAmount += reader.GetDecimal(5); // BuyPrice
                        }
                    }
                }
            }

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
            return RedirectToAction("Index", "Cart");
        }
    }

    public IActionResult PaymentSuccess(string paymentId, string token, string PayerID)
    {
        HttpContext.Session.Remove("Cart");
        TempData["SuccessMessage"] = "Payment completed successfully!";
        return RedirectToAction("Index", "HomePage");
    }

    public IActionResult PaymentCancel()
    {
        TempData["ErrorMessage"] = "Payment was canceled.";
        return RedirectToAction("Index", "Cart");
    }
}
