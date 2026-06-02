using Microsoft.AspNetCore.Authentication.Cookies;
using manitaDeGatoWeb.Data;

var builder = WebApplication.CreateBuilder(args);

// Configurar el DataDirectory y resolver |DataDirectory| en la cadena de conexion de forma manual
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "Data");
AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);

var connectionString = rawConnectionString.Replace("|DataDirectory|", dataDirectory);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(new DataBaseHelper(connectionString));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();



app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
