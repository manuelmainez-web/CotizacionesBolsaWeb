var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var app = builder.Build();

var invariantCultureOptions = new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CultureInfo.InvariantCulture),
    SupportedCultures = new[] { System.Globalization.CultureInfo.InvariantCulture },
    SupportedUICultures = new[] { System.Globalization.CultureInfo.InvariantCulture }
};
app.UseRequestLocalization(invariantCultureOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    var expectedUser = app.Configuration["BasicAuth:Username"];
    var expectedPassword = app.Configuration["BasicAuth:Password"];

    if (string.IsNullOrEmpty(expectedUser) || string.IsNullOrEmpty(expectedPassword))
    {
        await next();
        return;
    }

    // Solo se exige usuario/contraseña cuando el acceso llega desde fuera de la red local
    // (por ejemplo, a través de internet). El acceso por localhost o red local queda libre.
    if (IsLocalOrPrivateHost(context.Request.Host.Host))
    {
        await next();
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
