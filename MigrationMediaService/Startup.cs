using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MigrationMediaService.Services;
using System;

[assembly: FunctionsStartup(typeof(MigrationMediaService.Startup))]
namespace MigrationMediaService
{
    public class Startup : FunctionsStartup
    {
        private static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton(s =>
            {
                var connectionString = Configuration["CosmosDB"];
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        "Please specify a valid CosmosDB connection string in the appSettings.json file or your Azure Functions Settings.");
                }
                return new CosmosClientBuilder(connectionString).Build();
            });

            // Register StorageHandler as singleton.
            // A single instance will be created and reused
            // with every service request
            builder.Services.AddSingleton<StorageHandler>();

            // Register MediaHandler as singleton.
            // A single instance will be created and reused
            // with every service request
            builder.Services.AddSingleton<MediaHandler>();

            // Register HttpHandler as singleton.
            // A single instance will be created and reused
            // with every service request
            builder.Services.AddSingleton<HttpHandler>();
        }
    }
}
