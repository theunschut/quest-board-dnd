using System.Net;

namespace QuestBoard.IntegrationTests.Helpers;

public static class CookieAuthenticationHelper
{
    public static async Task<HttpClient> SignInAsync(
        WebApplicationFactory<Program> factory,
        UserEntity user,
        string password)
    {
        // Create client with cookie container to automatically handle cookies
        var cookieContainer = new CookieContainer();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,  // Allow redirects for login flow
            HandleCookies = false  // We'll handle cookies manually via container
        });

        // Create an HttpClientHandler with the cookie container
        var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = true,
            UseCookies = true
        };

        // Actually, the WebApplicationFactory client doesn't support replacing the handler
        // Let's use a simpler approach - use the default client which handles cookies automatically
        var defaultClient = factory.CreateClient();

        // Get the login page to extract anti-forgery token
        var loginPageResponse = await defaultClient.GetAsync("/Account/Login");
        var (token, _) = await AntiForgeryHelper.ExtractAntiForgeryTokenAsync(loginPageResponse);

        // Post login form with credentials
        var loginFormContent = AntiForgeryHelper.CreateFormContentWithAntiForgeryToken(
            new Dictionary<string, string>
            {
                ["Email"] = user.Email!,
                ["Password"] = password,
                ["RememberMe"] = "false"
            },
            token);

        var loginResponse = await defaultClient.PostAsync("/Account/Login", loginFormContent);

        // Check if login was successful (should redirect to home page or return URL)
        if (!loginResponse.IsSuccessStatusCode &&
            loginResponse.StatusCode != System.Net.HttpStatusCode.Redirect &&
            loginResponse.StatusCode != System.Net.HttpStatusCode.Found)
        {
            var loginContent = await loginResponse.Content.ReadAsStringAsync();
            throw new Exception($"Login failed with status {loginResponse.StatusCode}. Response: {loginContent.Substring(0, Math.Min(500, loginContent.Length))}");
        }

        // The client now has the authentication cookies automatically stored
        // Return the client for use in tests
        return defaultClient;
    }
}
