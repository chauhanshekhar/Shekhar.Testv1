using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.Infrastructure.Brokers.Groww;

public class GrowwHttpService : IGrowwHttpService
{
    private readonly HttpClient _http;
    private readonly IGrowwAuthService _auth;
    private readonly AppErrorLogger _errorLogger;
    private readonly IServiceScopeFactory _scopeFactory;


    // GLOBAL GROWW RATE LIMIT
    private static readonly SemaphoreSlim _apiLock = new(1, 1);

    private static DateTime _lastApiCall =
        DateTime.MinValue;


    public GrowwHttpService(
        HttpClient http,
        IGrowwAuthService auth,
        AppErrorLogger errorLogger,
        IServiceScopeFactory scopeFactory)
    {
        _http = http;
        _auth = auth;
        _errorLogger = errorLogger;
        _scopeFactory = scopeFactory;
    }


    public Task<GrowwHttpResult<T>> GetAsync<T>(
        string url,
        string? signalId = null,
        string? orderReferenceId = null)

        => SendAsync<T>(
            HttpMethod.Get,
            url,
            null,
            signalId,
            orderReferenceId);


    public Task<GrowwHttpResult<T>> PostAsync<T>(
        string url,
        object body,
        string? signalId = null,
        string? orderReferenceId = null)

        => SendAsync<T>(
            HttpMethod.Post,
            url,
            body,
            signalId,
            orderReferenceId);


    public Task<GrowwHttpResult<T>> PutAsync<T>(
        string url,
        object body,
        string? signalId = null,
        string? orderReferenceId = null)

        => SendAsync<T>(
            HttpMethod.Put,
            url,
            body,
            signalId,
            orderReferenceId);


    private async Task<GrowwHttpResult<T>> SendAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        string? signalId,
        string? orderReferenceId)
    {
        var token = _auth.GetAccessToken();

        GrowwHttpClientHelper.SetAuth(
            _http,
            token);

        var requestJson =
            body != null
            ? JsonSerializer.Serialize(body)
            : null;


        try
        {
            using var request =
                new HttpRequestMessage(
                    method,
                    url);


            if (requestJson != null)
            {
                request.Content =
                    new StringContent(
                        requestJson,
                        Encoding.UTF8,
                        "application/json");
            }


            using var response =
                await SendWithRateLimitAsync(
                    request);


            var responseJson =
                await response.Content
                .ReadAsStringAsync();



            // SUCCESS
            if (response.IsSuccessStatusCode)
            {
                var data =
                    GrowwJsonHelper
                    .Deserialize<T>(
                        responseJson);


                return new GrowwHttpResult<T>
                {
                    IsSuccess = true,
                    Data = data,
                    StatusCode =
                        (int)response.StatusCode,

                    ResponseBody =
                        responseJson
                };
            }



            // 429 HANDLING
            if (response.StatusCode ==
          HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(10));

                return new GrowwHttpResult<T>
                {
                    IsSuccess = false,
                    IsAmbiguous = true,
                    StatusCode = 429,
                    ErrorMessage = "Groww rate limit"
                };
            }




            var message =
                $"Groww API error {(int)response.StatusCode}";


            await LogFailureAsync(
                method,
                url,
                token,
                requestJson,
                responseJson,
                (int)response.StatusCode,
                message,
                signalId,
                orderReferenceId);



            return new GrowwHttpResult<T>
            {
                IsSuccess = false,

                IsAmbiguous =
                    (int)response.StatusCode >= 500,

                StatusCode =
                    (int)response.StatusCode,

                ErrorMessage =
                    $"{message}: {Truncate(responseJson, 500)}",

                ResponseBody =
                    responseJson
            };

        }
        catch (Exception ex)
        when (
            ex is HttpRequestException
            or TaskCanceledException
            or OperationCanceledException)
        {

            await LogFailureAsync(
                method,
                url,
                token,
                requestJson,
                ex.Message,
                null,
                ex.Message,
                signalId,
                orderReferenceId,
                ex);


            return new GrowwHttpResult<T>
            {
                IsSuccess = false,
                IsAmbiguous = true,
                ErrorMessage =
                    ex.Message
            };
        }
    }



    private async Task<HttpResponseMessage>
        SendWithRateLimitAsync(
            HttpRequestMessage request)
    {

        await _apiLock.WaitAsync();


        try
        {

   

            const int GrowwDelayMs = 2500;

            var diff = DateTime.UtcNow - _lastApiCall;

            if (diff.TotalMilliseconds < GrowwDelayMs)
            {
                await Task.Delay(
                    GrowwDelayMs - (int)diff.TotalMilliseconds);
            }

            _lastApiCall = DateTime.UtcNow;


            return await
                _http.SendAsync(request);

        }
        finally
        {
            _apiLock.Release();
        }
    }





    private async Task LogFailureAsync(
        HttpMethod method,
        string url,
        string token,
        string? requestJson,
        string? responseBody,
        int? statusCode,
        string message,
        string? signalId,
        string? orderReferenceId,
        Exception? ex = null)
    {

        var cutoff =
            IstClock.Now.AddMinutes(-5);


        using (var scope =
            _scopeFactory.CreateScope())
        {

            var repo =
                scope.ServiceProvider
                .GetRequiredService<IRepository<AppError>>();


            var recent =
                await repo.FindAsync(e =>
                    e.Source ==
                    "GrowwHttpService"

                    && e.RequestUrl == url

                    && e.Message == message

                    && e.OccurredOn >= cutoff);


            if (recent.Count > 0)
                return;
        }



        var context =
            JsonSerializer.Serialize(
            new
            {
                broker = "GROWW",

                method =
                    method.Method,

                url,

                curl =
                    BuildCurl(
                        method,
                        url,
                        token,
                        requestJson),

                request =
                    requestJson,

                response =
                new
                {
                    statusCode,

                    body =
                    Truncate(
                        responseBody,
                        2000)
                },

                orderReferenceId,

                signalId
            });



        await _errorLogger.LogAsync(
            "GrowwHttpService",
            ex ??
            new InvalidOperationException(message),
            context: context,
            requestUrl: url);
    }





    private static string BuildCurl(
        HttpMethod method,
        string url,
        string token,
        string? requestJson)
    {

        var sb =
            new StringBuilder(
                $"curl -X {method.Method} '{url}'");


        sb.Append(
            $" -H 'Authorization: Bearer {MaskToken(token)}'");


        sb.Append(
            " -H 'Content-Type: application/json'");


        if (!string.IsNullOrEmpty(requestJson))
        {
            sb.Append(
                $" -d '{requestJson}'");
        }


        return sb.ToString();
    }




    private static string MaskToken(
        string token)
    {

        if (string.IsNullOrEmpty(token))
            return "";


        return token.Length <= 5
            ? "********"
            : $"********{token[^5..]}";
    }




    private static string? Truncate(
        string? s,
        int max)
    {

        return
            s == null ||
            s.Length <= max

            ? s

            : s[..max] + "…";
    }
}