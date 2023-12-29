﻿using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NoMercy.Server;
using NoMercy.TMDBApi.Client;
using System.Text.Json.Serialization;

namespace NoMercy
{
    public class Startup
    {
        public static ApiInfo? ApiInfo;
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });

            if (HybridSupport.IsElectronActive)
            {
                ElectronBootstrap();
            }
        }

        public async void ElectronBootstrap()
        {
            //AddDevelopmentTests();

            var browserWindow = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
            {
                Width = 1152,
                Height = 940,
                Show = false
            });

            await browserWindow.WebContents.Session.ClearCacheAsync();

            browserWindow.OnReadyToShow += () => browserWindow.Show();

            var tvClient = new TvClient(1433);
            var result = await tvClient.Details();

            Console.WriteLine(JsonConvert.SerializeObject(result));
        }

        private static void AddDevelopmentTests()
        {
            // NOTE: on mac you will need to allow the app to post notifications when asked.

            Electron.App.On("activate", (obj) =>
            {
                // obj should be a boolean that represents where there are active windows or not.
                var hasWindows = (bool)obj;

                Electron.Notification.Show(
                    new NotificationOptions("Activate", $"activate event has been captured. Active windows = {hasWindows}")
                    {
                        Silent = false,
                    });
            });

            Electron.Dock.SetMenu(new[]
            {
                new MenuItem
                {
                    Type = MenuType.normal,
                    Label = "MenuItem",
                    Click = () =>
                    {
                        Electron.Notification.Show(new NotificationOptions(
                            "Dock MenuItem Click",
                            "A menu item added to the Dock was selected;"));
                    },
                },
                new MenuItem
                {
                    Type = MenuType.submenu,
                    Label = "SubMenu",
                    Submenu = new[]
                    {
                        new MenuItem
                        {
                            Type = MenuType.normal,
                            Label = "Sub MenuItem",
                            Click = () =>
                            {
                                Electron.Notification.Show(new NotificationOptions(
                                    "Dock Sub MenuItem Click",
                                    "A menu item added to the Dock was selected;"));
                            },
                        },
                    }
                }
            });
        }
    }
}