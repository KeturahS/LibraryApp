using System.Text;
using System.Security.Cryptography;


namespace LibraryApp.Helpers
{
    public static class HashHelper
    {
        public static string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();

                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2")); // הופך לבסיס 16

                return builder.ToString();
            }
        }
    }

}
