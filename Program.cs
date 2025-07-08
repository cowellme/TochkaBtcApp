using Microsoft.AspNetCore.Authentication.Cookies;
using TochkaBtcApp.Telegram;

namespace TochkaBtcApp
{
    public class Program
    {
        private static TBot? _tBot;
        public static void Main(string[] args)
        {
            //var task = Models.Exc.BingX.Checker();
            var builder = WebApplication.CreateBuilder(args);

            //My
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder =>
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });
            builder.Services.AddSingleton<ApplicationContext>();
            builder.Services.AddSingleton<TBot>();
            _tBot = new TBot();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(
                options =>
                {
                    options.Cookie.Name = "auth_token";
                    options.LoginPath = "/login";
                    options.Cookie.MaxAge = TimeSpan.FromDays(5);
                    options.AccessDeniedPath = "/access-denied";
                });
            builder.Services.AddAuthorization();
            builder.Services.AddCascadingAuthenticationState();
            

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();



            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<Components.App>()
                .AddInteractiveServerRenderMode();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.MapControllers();
            app.Run();
        }
    }
}
