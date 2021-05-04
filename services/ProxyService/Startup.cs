using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;

namespace ProxyService
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Add the reverse proxy to capability to the server
            var proxyBuilder = services.AddReverseProxy();
            
            // Initialize the reverse proxy from the "ReverseProxy" section of configuration
            proxyBuilder.LoadFromConfig(Configuration.GetSection("ReverseProxy"));

            if (System.Environment.GetEnvironmentVariable("CLUSTER_MODE").ToLower() == "true")
            {
                // See: https://docs.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme#environment-variables
                TokenCredential credential = new DefaultAzureCredential();
                string blobContainerUri = System.Environment.GetEnvironmentVariable("DATAPROTECTION_STORAGE_CONTAINER_URI");
                string keyVaultKeyUri = System.Environment.GetEnvironmentVariable("DATAPROTECTION_KEY_URI");
                services.AddDataProtection().PersistKeysToAzureBlobStorage(new Uri(blobContainerUri), credential).ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyUri), credential);
            }

            services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(options =>
            {
                options.Cookie.Name = ".ProxyData";
                options.Cookie.HttpOnly = true;
                options.Cookie.Expiration = TimeSpan.FromHours(8);
            });
            
            services.AddControllersWithViews();
            services.AddSingleton<SessionTracker>();
            services.Configure<RateLimitMiddlewareOptions>(options => { Configuration.Bind(options); } );
            services.Configure<SessionTrackerOptions>(options => { Configuration.Bind(options); } );

            // Load HTML page
            byte[] html = File.ReadAllBytes(Configuration["HTML_FILENAME"]);

            // Telemetry
            services.AddApplicationInsightsTelemetry();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                // Health check URI
                endpoints.MapGet("/api/health", async context =>
                {
                    await context.Response.WriteAsync("running");
                });

                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    if (System.Environment.GetEnvironmentVariable("MIDDLEWARE_ENABLED").ToLower() == "true")
                        proxyPipeline.UseMiddleware<RateLimitMiddleware>();
                    
                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    // proxyPipeline.UseAffinitizedDestinationLookup();
                    // proxyPipeline.UseProxyLoadBalancing();
                });
            });
        }

    }

}
