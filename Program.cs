using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var protectionPath = System.IO.Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
System.IO.Directory.CreateDirectory(protectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(protectionPath));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRazorPages();

var app = builder.Build();

app.UseForwardedHeaders();

var spanishCulture = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
var localizationOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(spanishCulture, spanishCulture),
    SupportedCultures = new[] { spanishCulture },
    SupportedUICultures = new[] { spanishCulture }
};
app.UseRequestLocalization(localizationOptions);

System.Globalization.CultureInfo.DefaultThreadCurrentCulture = spanishCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = spanishCulture;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();

    var disableHttpsRedirect = app.Configuration.GetValue<bool>("DisableHttpsRedirection");
    if (!disableHttpsRedirect)
    {
        app.UseHttpsRedirection();
    }
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
        return Task.CompletedTask;
    });

    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "0";
    }
});
app.UseRouting();

app.Use(async (context, next) =>
{
    var requireExternalBasicAuth = app.Configuration.GetValue<bool>("BasicAuth:RequireExternalAccess");
    var expectedUser = app.Configuration["BasicAuth:Username"];
    var expectedPassword = app.Configuration["BasicAuth:Password"];
    var requestPath = context.Request.Path.Value ?? string.Empty;
    var queryString = context.Request.QueryString.Value ?? string.Empty;
    var isPublicPageOrTransactionEndpoint =
        string.Equals(requestPath, "/", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(requestPath, "/Index", StringComparison.OrdinalIgnoreCase) ||
        queryString.Contains("handler=TransactionState", StringComparison.OrdinalIgnoreCase) ||
        queryString.Contains("handler=SaveTransactionState", StringComparison.OrdinalIgnoreCase);

    if (!requireExternalBasicAuth || isPublicPageOrTransactionEndpoint)
    {
        await next();
        return;
    }

    // El acceso por localhost o red local queda libre.
    if (IsLocalOrPrivateHost(context.Request.Host.Host))
    {
        await next();
        return;
    }

    if (string.IsNullOrEmpty(expectedUser) || string.IsNullOrEmpty(expectedPassword))
    {
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Cotizaciones Bolsa\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var authHeader = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var encodedCredentials = authHeader["Basic ".Length..].Trim();
            var decodedBytes = Convert.FromBase64String(encodedCredentials);
            var decodedCredentials = System.Text.Encoding.UTF8.GetString(decodedBytes);
            var separatorIndex = decodedCredentials.IndexOf(':');

            if (separatorIndex > 0)
            {
                var user = decodedCredentials[..separatorIndex];
                var password = decodedCredentials[(separatorIndex + 1)..];

                if (user == expectedUser && password == expectedPassword)
                {
                    await next();
                    return;
                }
            }
        }
        catch (FormatException)
        {
            // Cabecera Authorization mal formada: se trata como no autenticado.
        }
    }

    context.Response.Headers.WWWAuthenticate = "Basic realm=\"Cotizaciones Bolsa\"";
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();

static bool IsLocalOrPrivateHost(string host)
{
    if (string.IsNullOrWhiteSpace(host))
    {
        return true;
    }

    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!System.Net.IPAddress.TryParse(host, out var ip))
    {
        // Host no es una IP (p.ej. dominio p\u00fablico): se trata como externo.
        return false;
    }

    if (System.Net.IPAddress.IsLoopback(ip))
    {
        return true;
    }

    var bytes = ip.GetAddressBytes();
    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    {
        // 10.0.0.0/8
        if (bytes[0] == 10)
        {
            return true;
        }

        // 172.16.0.0/12
        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        // 192.168.0.0/16
        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }
    }

    return false;
}
