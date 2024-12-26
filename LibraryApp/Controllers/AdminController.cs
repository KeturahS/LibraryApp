using LibraryApp.Models;
using LibraryApp.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using static LibraryApp.Models.ConnectionToDBmodel;

namespace LibraryApp.Controllers
{
    public class AdminController : Controller
    {

        private readonly IConfiguration _configuration;
        string connectionString;
        public AdminController(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = configuration.GetConnectionString("myConnect");


        }


        public IActionResult ShowPendingAdminRequests()
        {

            RequestedToBeAdminModel requestedToBeAdminModel = new RequestedToBeAdminModel();
            requestedToBeAdminModel.Users = new List<User>();
            requestedToBeAdminModel.Users = GetPendingAdminRequests();

            return View("Admin_Home_Page", requestedToBeAdminModel);
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
        public IActionResult ApproveReject(string userId, string action)
        {
            if (action == "approve")
            {

                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Admin" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteQuery<int>(updateQuery, parameters);




            }
            else if (action == "reject")
            {
                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Rejected" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteQuery<int>(updateQuery, parameters);


            }

            return View("Admin_Home_Page");
        }


    }
}
