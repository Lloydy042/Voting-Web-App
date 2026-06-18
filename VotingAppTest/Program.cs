using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;
using QuestPDF.Infrastructure;
using VotingAppTest.Models; // namespace for MongoDbContext
using System.Security.Claims;
public class Program
{
    public static void Main(string[] args)
      {
        // Configure QuestPDF license
        QuestPDF.Settings.License = LicenseType.Community;

        var builder = WebApplication.CreateBuilder(args);

        // Register MongoDbContext with DI
        builder.Services.AddSingleton<MongoDbContext>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new MongoDbContext(config);
        });

        // Register EmailService
        builder.Services.AddSingleton<EmailService>();

        builder.Services.AddControllersWithViews();
        builder.Services.AddSession();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }
    //    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    //.AddCookie(options =>
    //{
    //    options.LoginPath = "/Home/Login";
    //    options.AccessDeniedPath = "/Home/Login";
    //});

        app.UseStaticFiles();
        app.UseSession();
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Landing}/{id?}");

        app.Run();
    }
}

