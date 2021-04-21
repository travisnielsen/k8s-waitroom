using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            // Enable sessions
            // services.AddDistributedMemoryCache();

            services.AddSingleton<ITempDataProvider, CookieTempDataProvider>();
            services.AddControllersWithViews();

            services.AddSession(options =>
            {
                options.Cookie.Name = ".WaitRoom.Session";
                options.IdleTimeout = TimeSpan.FromHours(1);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddSingleton<SessionTracker>();
            services.Configure<RateLimitMiddlewareOptions>(options => { Configuration.Bind(options); } );
            services.Configure<SessionTrackerOptions>(options => { Configuration.Bind(options); } );

            // Load HTML page
            byte[] html = File.ReadAllBytes(Configuration["HTML_FILENAME"]);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseSession();
            app.UseEndpoints(endpoints =>
            {
                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    proxyPipeline.UseMiddleware<RateLimitMiddleware>();
                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    // proxyPipeline.UseAffinitizedDestinationLookup();
                    // proxyPipeline.UseProxyLoadBalancing();
                });
            });
        }

    }

}
