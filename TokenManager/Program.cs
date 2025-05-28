using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    private static string _accessToken;
    private static DateTime _expiryTime;
    private static int _requestCount = 0;
    private static DateTime _hourWindow = DateTime.UtcNow;
    private static Timer _timer;

    static async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _expiryTime)
            return _accessToken;

        if ((DateTime.UtcNow - _hourWindow).TotalHours >= 1)
        {
            _requestCount = 0;
            _hourWindow = DateTime.UtcNow;
        }

        if (_requestCount >= 5)
            throw new Exception("Saatlik token sınırına ulaşıldı!");

        using var client = new HttpClient();

        var response = await client.PostAsync("https://api.example.com/token", new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", "xxx"),
            new KeyValuePair<string, string>("client_secret", "yyy"),
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        }));

        if (!response.IsSuccessStatusCode)
            throw new Exception("Token alınamadı: " + response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        _accessToken = root.GetProperty("access_token").GetString();
        int expiresIn = root.GetProperty("expires_in").GetInt32();
        _expiryTime = DateTime.UtcNow.AddSeconds(expiresIn - 60); // Güvenlik için 1 dk erken bitmiş say

        _requestCount++;

        Console.WriteLine($"Yeni token alındı. Süresi: {_expiryTime:HH:mm:ss}");
        return _accessToken;
    }

    static async void TimerCallback(object state)
    {
        try
        {
            string token = await GetTokenAsync();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("https://api.example.com/orders");

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[✓] Siparişler çekildi: {data.Substring(0, Math.Min(100, data.Length))}...");
            }
            else
            {
                Console.WriteLine($"[x] Sipariş çekilemedi: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[HATA] " + ex.Message);
        }
    }

    static void Main(string[] args)
    {
        Console.WriteLine("Program başladı. Her 5 dakikada bir sipariş verisi çekilecek.");
        _timer = new Timer(TimerCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        Console.ReadLine(); // Programın çalışmaya devam etmesi için
    }
}
