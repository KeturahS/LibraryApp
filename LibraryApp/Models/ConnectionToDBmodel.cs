﻿using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace LibraryApp.Models
{
    public class ConnectionToDBmodel
    {
        public class ConnectionToDBModel
        {
            private readonly IConfiguration _configuration;
            private readonly string _connectionString;

            public ConnectionToDBModel(IConfiguration configuration)
            {
                _configuration = configuration;
                _connectionString = _configuration.GetConnectionString("myConnect");
            }


            public List<T> ExecuteQuery<T>(string query, Dictionary<string, object> parameters = null, Func<SqlDataReader, T> mapper = null)
            {
                using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
                {
                    connection.Open();


                    var results = new List<T>();

                    using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(query, connection))
                    {
                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                            }
                        }

                        // If a mapper is provided, assume it's a SELECT query
                        if (mapper != null)
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    results.Add(mapper(reader));
                                }
                            }


                        }
                     
                    }

                    return results;
                }
            }


			public int ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
			{
				using (SqlConnection connection = new SqlConnection(_connectionString))
				{
					connection.Open();

					using (SqlCommand command = new SqlCommand(query, connection))
					{
						if (parameters != null)
						{
							foreach (var param in parameters)
							{
								command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
							}
						}

						return command.ExecuteNonQuery(); // Returns the number of affected rows
					}
				}
			}



            public T ExecuteScalar<T>(string query, Dictionary<string, object> parameters)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    // Open the connection
                    connection.Open();

                    using (var command = new SqlCommand(query, connection))
                    {
                        // Add parameters to the command
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                        }

                        // Execute the query and get the scalar result
                        var result = command.ExecuteScalar();

                        // Handle null results
                        if (result == null || result == DBNull.Value)
                        {
                            return default; // Return default value for the type T
                        }

                        // Convert and return the result to the desired type
                        return (T)Convert.ChangeType(result, typeof(T));
                    }
                }
            }











        }
    }
}
     


        
    

