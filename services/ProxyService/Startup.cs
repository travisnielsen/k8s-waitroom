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
        private int NEW_SESSION_WINDOW_SECS;
        private int MAX_NEW_SESSIONS_IN_WINDOW;
        private string REDIRECT_URL;

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

            // TODO: Consider throwing an exteption and shutting down if env vars are not set. Difficult to assume defaults for a given environment.

            // Load new session regulation settings from env
            bool parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("NEW_SESSION_WINDOW_SECS"), out NEW_SESSION_WINDOW_SECS);
            if (!parseSuccess)
                NEW_SESSION_WINDOW_SECS = 60;
            
            parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("MAX_NEW_SESSIONS_IN_WINDOW"), out MAX_NEW_SESSIONS_IN_WINDOW);
            if (!parseSuccess)
                MAX_NEW_SESSIONS_IN_WINDOW = 5;

            REDIRECT_URL = Environment.GetEnvironmentVariable("REDIRECT_URL");
            if (String.IsNullOrEmpty(REDIRECT_URL))
                REDIRECT_URL = "https://exclusive.website";

            SessionTracker.WindowBeginTime = DateTime.Now;
            SessionTracker.CurrentNewSessions = 0;

            // Enable sessions
            services.AddDistributedMemoryCache();

            services.AddSession(options =>
            {
                options.Cookie.Name = ".WaitRoom.Session";
                options.IdleTimeout = TimeSpan.FromHours(1);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });
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
            // Set up new SessionTracker values if empty or expired
            if (DateTime.Now.Subtract(SessionTracker.WindowBeginTime).TotalSeconds > NEW_SESSION_WINDOW_SECS)
            {
                SessionTracker.WindowBeginTime = DateTime.Now;
                SessionTracker.CurrentNewSessions = 0;
                Console.WriteLine("New session window starting at: " + SessionTracker.WindowBeginTime.ToLocalTime());
            }

            if (context.Session.GetString("_name") != null)
            {
                // The user has an exisitng proxy session. This is not a new connection. Forward the request to the backend.
                Console.WriteLine("Existing session: " + context.Session.Id);
                return next();
            }

            // Check if the user has any waitroom session data. Empty / default sessions are pased into HttpContext on each new call.
            // If we are under quota and no existing session cookie exists. Create one.
            if (string.IsNullOrEmpty(context.Session.GetString("_name")) && SessionTracker.CurrentNewSessions < MAX_NEW_SESSIONS_IN_WINDOW)
            {
                // Writing a value triggers writing the cookie to preserve session affiation on subsequent calls.
                context.Session.SetString("_name", "waitroom");
                SessionTracker.CurrentNewSessions += 1;
                Console.WriteLine("New session: " + context.Session.Id);
                Console.WriteLine("Current new sessions: " + SessionTracker.CurrentNewSessions);
                return next();
            }
            
            if (SessionTracker.CurrentNewSessions == MAX_NEW_SESSIONS_IN_WINDOW)
            {
                // At or exceeded quota for the new session window. Redirect to the virtual wait room
                // context.Response.WriteAsJsonAsync("test");
                context.Response.Redirect(REDIRECT_URL);
                return context.Response.StartAsync();
            }
            
            return next();
        }

    }

    static class SessionTracker
    {
        public static DateTime WindowBeginTime { get; set; }
        public static int CurrentNewSessions { get; set; }
    }

}
