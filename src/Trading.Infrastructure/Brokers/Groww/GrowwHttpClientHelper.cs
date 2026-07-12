using System.Net.Http.Headers;

namespace Trading.Infrastructure.Brokers.Groww;

public static class GrowwHttpClientHelper
{
    public static void ConfigureClient(HttpClient http, GrowwSettings settings)
    {
        http.BaseAddress = new Uri(settings.BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-VERSION", "1.0");
    }

    public static void SetAuth(HttpClient http, string accessToken)
    {
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
