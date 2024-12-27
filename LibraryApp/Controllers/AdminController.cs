using LibraryApp.Models;
using LibraryApp.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Reflection;
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


        public IActionResult ShowAdminPage()
        {

            AdminViewModel AdminPageModel = new AdminViewModel();
            AdminPageModel.AdminUserRequests = new List<User>();
            AdminPageModel.AdminUserRequests = GetPendingAdminRequests();
            AdminPageModel.AllUsers = new List<User>();
            AdminPageModel.AllUsers = GetAllUsers();
            AdminPageModel.NewUser= new User(); 


            return View("Admin_Home_Page", AdminPageModel);
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
                connection.ExecuteQuery<int>(updateQuery, parameters);




            }
            
            if (action == "reject")
            {
                var updateQuery = "UPDATE Users_tbl SET Status = @NewStatus WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                     { "@NewStatus", "Rejected" },
                     { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteQuery<int>(updateQuery, parameters);


            }


            if (action == "Delete")
            {
                var updateQuery = "DELETE FROM Users_tbl WHERE email = @Email";

                var parameters = new Dictionary<string, object>{
                        { "@Email", userId }};

                // Execute the update using ExecuteQuery
                ConnectionToDBModel connection = new ConnectionToDBModel(_configuration);
                connection.ExecuteQuery<int>(updateQuery, parameters);


            }




       


            return RedirectToAction("ShowAdminPage");
        }


        public IActionResult AddUser()
        {

         
          

           return View();
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


    }
}
