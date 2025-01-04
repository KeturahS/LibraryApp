using Microsoft.AspNetCore.Http;
using System.Text.Json;

public static class SessionExtensions
{
    // מתודה לשמירת אובייקט ב-Session
    public static void SetObject<T>(this ISession session, string key, T value)
    {
        session.SetString(key, JsonSerializer.Serialize(value));
        Console.WriteLine($"Object saved to session with key: {key}");
    }

    // מתודה לקבלת אובייקט מה-Session
    public static T GetObject<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        if (value == null)
        {
            Console.WriteLine($"No object found in session with key: {key}");
            return default;
        }

        try
        {
            var deserializedObject = JsonSerializer.Deserialize<T>(value);
            Console.WriteLine($"Object retrieved from session with key: {key}, Value: {value}");
            return deserializedObject;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error deserializing object from session with key: {key}, Error: {ex.Message}");
            return default;
        }
    }
}
