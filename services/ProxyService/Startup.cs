using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Middleware;
using Yarp.ReverseProxy.RuntimeModel;

namespace ProxyService
{
    public class Startup
    {
        private const string DEBUG_HEADER = "Debug";
        private const string DEBUG_METADATA_KEY = "debug";
        private const string DEBUG_VALUE = "true";
        private int NEW_SESSION_WINDOW_SECS;
        private int MAX_NEW_SESSIONS_IN_WINDOW;

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

            // Load new session regulation settings from env
            bool parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("NEW_SESSION_WINDOW_SECS"), out NEW_SESSION_WINDOW_SECS);
            if (!parseSuccess)
                NEW_SESSION_WINDOW_SECS = 60;
            
            parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("MAX_NEW_SESSIONS_IN_WINDOW"), out MAX_NEW_SESSIONS_IN_WINDOW);
            if (!parseSuccess)
                MAX_NEW_SESSIONS_IN_WINDOW = 5;

            SessionTracker.WindowBeginTime = DateTime.Now;
            SessionTracker.CurrentNewSessions = 0;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // We can customize the proxy pipeline and add/remove/replace steps
                endpoints.MapReverseProxy(proxyPipeline =>
                {
                    // Use a custom proxy middleware, defined below
                    proxyPipeline.Use(MyCustomProxyStep);

                    // Don't forget to include these two middleware when you make a custom proxy pipeline (if you need them).
                    // proxyPipeline.UseAffinitizedDestinationLookup();
                    // proxyPipeline.UseProxyLoadBalancing();
                });
            });
        }

        /// <summary>
        /// Custom proxy step to issue a 302 redirect if new session limit is exceeded
        /// </summary>
        public Task MyCustomProxyStep(HttpContext context, Func<Task> next)
        {
            // Can read data from the request via the context
            // var useDebugDestinations = context.Request.Headers.TryGetValue(DEBUG_HEADER, out var headerValues) && headerValues.Count == 1 && headerValues[0] == DEBUG_VALUE;

            // Set up new SessionTracker values if empty or expired
            var test = DateTime.Now.Subtract(SessionTracker.WindowBeginTime).TotalSeconds;

            if (DateTime.Now.Subtract(SessionTracker.WindowBeginTime).TotalSeconds > NEW_SESSION_WINDOW_SECS)
            {
                SessionTracker.WindowBeginTime = DateTime.Now;
                SessionTracker.CurrentNewSessions = 0;
                Console.WriteLine("New session starting at: " + SessionTracker.WindowBeginTime.ToLocalTime());
            }

            SessionTracker.CurrentNewSessions += 1;
            Console.WriteLine("Current new sessions: " + SessionTracker.CurrentNewSessions);

            if (SessionTracker.CurrentNewSessions >= MAX_NEW_SESSIONS_IN_WINDOW)
            {
                context.Response.Redirect("https://bing.com");
                return context.Response.StartAsync();
            }
            else
            {
                return next();
            }
        }

    }

    static class SessionTracker
    {
        public static DateTime WindowBeginTime { get; set; }
        public static int CurrentNewSessions { get; set; }
    }

}
