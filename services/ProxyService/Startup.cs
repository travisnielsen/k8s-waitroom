using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Middleware;
using Yarp.ReverseProxy.RuntimeModel;

namespace ProxyService
{
    public class Startup
    {
        private int NEW_SESSION_WINDOW_SECS;
        private int MAX_NEW_SESSIONS_IN_WINDOW;
        private string REDIRECT_URL;
        private int NEW_SESSION_BLOCK_DURATION_MINS;
        private ILogger _logger;
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
            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.Cookie.Name = ".WaitRoom.Session";
                options.IdleTimeout = TimeSpan.FromHours(1);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            services.AddSingleton<SessionTracker>();
            services.Configure<RateLimitOptions>(options => { Configuration.Bind(options); } );
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
