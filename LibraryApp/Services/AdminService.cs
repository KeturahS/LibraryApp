using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using LibraryApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using static LibraryApp.Models.ConnectionToDBmodel;



namespace LibraryApp.Services
{
    public class AdminService : IAdminService
    {
        private readonly IConfiguration _configuration;
        string connectionString;

        public AdminService(IConfiguration configuration)
        {
            _configuration = configuration;
            connectionString = configuration.GetConnectionString("myConnect");
        }

        public void SendBorrowingReminders()
        {
            
                // SQL query to find borrowed books that are due in 5 day
                string query = @"
            SELECT UserName, BookTitle,ReturnDate,Author,Publisher,YearOfPublication,BorrowID
            FROM BorrowedBooks
            WHERE DATEDIFF(DAY, GETDATE(), ReturnDate) = 5";

                var usersToRemind = new List<BorrowedBook>();

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            while (reader.Read())
                            {
                                usersToRemind.Add(new BorrowedBook
                                {
                                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                                    BookTitle = reader.GetString(reader.GetOrdinal("BookTitle")),
                                    Author = reader.GetString(reader.GetOrdinal("Author")),
                                    Publisher = reader.GetString(reader.GetOrdinal("Publisher")),
                                    YearOfPublication = reader.GetInt32(reader.GetOrdinal("YearOfPublication")),
                                    ReturnDate = reader.GetDateTime(reader.GetOrdinal("ReturnDate")),
                                    BorrowID = reader.GetInt32(reader.GetOrdinal("BorrowID"))
                                });
                            }
                        }
                    }
                }

                // שליחת מיילים לכל המשתמשים שצריך להזכיר להם
                foreach (var item in usersToRemind)
                {
                    Console.WriteLine($"Sending reminder to {item.UserName} for book '{item.BookTitle}'...");

                    Gmail gmail = new Gmail
                    {
                        To = item.UserName,
                        Subject = "Reminder: Your book borrowing period is ending soon!",
                        Body = $"Dear {item.UserName},\n\n" +
                               $"This is a reminder that the book '{item.BookTitle}' you borrowed is due to be returned on {item.ReturnDate:dd/MM/yyyy}. " +
                               "Please make sure to return it on time.\n\nThank you."
                    };

                    gmail.SendEmail();
                }

                
            }
          
    }
}



