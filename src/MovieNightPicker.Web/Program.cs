using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MovieNightPicker.Web;
using MovieNightPicker.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Auth plumbing: token storage, JWT-backed auth state, and a handler that attaches
// the bearer token to every API call.
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<BearerTokenHandler>();

// The API base URL comes from wwwroot/appsettings.json (Api:BaseUrl). The default
// HttpClient is the authenticated API client, so feature components just inject HttpClient.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5196";
builder.Services
    .AddHttpClient("Api", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

builder.Services.AddScoped<AuthClient>();

await builder.Build().RunAsync();
