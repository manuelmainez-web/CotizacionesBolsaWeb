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

app.UseAuthorization();

app.MapRazorPages();

app.Run();
