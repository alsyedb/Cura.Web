using Cura.Web.Data;
using Cura.Web.Services;

namespace Cura.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Bind Together config
            builder.Services.Configure<TogetherOptions>(builder.Configuration.GetSection("Together"));
            // HttpClient for AI
            builder.Services.AddHttpClient<IAiClient, TogetherAiClient>();
            // In-memory data
     //       builder.Services.AddSingleton<IPatientRepo, InMemoryPatientRepo>();
            builder.Services.AddSingleton<IPatientRepo, FileFhirPatientRepo>();

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
