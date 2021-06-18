using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaPackageAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (urls == null)
            {
                var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
                urls = $"http://0.0.0.0:{port}";
            }

            CreateHostBuilder(urls, args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string urls, string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => {
                    webBuilder
                    .UseStartup<Startup>()
                    .UseUrls(urls);
                });
    }
}