using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Services;
using System.Configuration;

namespace ProyectoLogin
{
    public class Program
    {
        public static void Main(string[] args)
        {



            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            var connectionStringAfiliacionesDBContext = builder.Configuration.GetConnectionString("AfiliacionesDBContext");


            builder.Services.AddDbContext<UsuarioContext>(o =>
            {
                o.UseSqlServer(builder.Configuration.GetConnectionString("CadenaSQL"));
            });


            builder.Services.AddScoped<IUsuarioService, UsuarioService>();
            builder.Services.AddScoped<IFilesService, FilesService>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Login/IniciarSesion";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = builder.Configuration.GetSection("GoogleKeys:ClientId").Value;
                options.ClientSecret = builder.Configuration.GetSection("GoogleKeys:ClientSecret").Value;
            })
            .AddFacebook(FacebookDefaults.AuthenticationScheme, options => // Configuración de Facebook
            {
                options.AppId = builder.Configuration.GetSection("FacebookKeys:AppId").Value;
                options.AppSecret = builder.Configuration.GetSection("FacebookKeys:AppSecret").Value;
            });

            //builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            //    .AddCookie(options =>
            //    {
            //        options.LoginPath = "/Login/IniciarSesion";
            //        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
            //    });
            builder.Services.AddSession();
            builder.Services.AddHttpContextAccessor(); // Agregar el servicio HttpContextAccessor

            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.Add(
                    new ResponseCacheAttribute
                    {
                        NoStore = true,
                        Location = ResponseCacheLocation.None,
                    }
                   );
            });

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
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Login}/{action=IniciarSesion}/{id?}");

            app.Run();
        }
    }
}