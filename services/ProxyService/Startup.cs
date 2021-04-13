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
        private SessionTracker Tracker;
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, ILogger<Startup> logger)
        {
            _logger = logger;

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

            // Load settings from environment variables
            try
            {
                LoadSessionParams();
                Tracker = new SessionTracker();
            }
            catch (Exception e)
            {
                _logger.LogCritical(e.Message);
                throw new ApplicationException(e.Message);
                // Console.WriteLine(e.Message);
            }

        }

        /// <summary>
        /// Custom proxy step to issue a 302 redirect if new session limit is exceeded
        /// </summary>
        public Task MyCustomProxyStep(HttpContext context, Func<Task> next)
        {
            // Any connection with a valid session cookie is allowed
            if (!string.IsNullOrEmpty(context.Session.GetString("_name")))
            {
                _logger.LogInformation("Existing session: " + context.Session.Id);
                // Console.WriteLine("Existing session: " + context.Session.Id);
                return next();
            }

            // Remove session block if its expired
            if (Tracker.SessionBlockActive && Tracker.SessionBlockIsExpired())
            {
                Tracker.SessionBlockActive = false;
                _logger.LogInformation("Session block removed: " + DateTime.Now.ToLocalTime());
                // Console.WriteLine("Session block removed: " + DateTime.Now.ToLocalTime());
            }

            // Deny new users during an active session block
            if (Tracker.SessionBlockActive && !Tracker.SessionBlockIsExpired())
            {
                if (context.Session.GetString("_name") == null)
                {
                    // This is a new user connecting during an active session block
                    context.Response.Redirect(REDIRECT_URL);
                    return Task.CompletedTask;
                }
            }
            
            // Create new session window if current one is expired
            Tracker.RefreshNewSessionWindow(NEW_SESSION_WINDOW_SECS);

            // Check if the user has any waitroom session data. Empty / default sessions are pased into HttpContext on each new call.
            // If we are under quota and no existing session cookie exists. Create one.
            if (string.IsNullOrEmpty(context.Session.GetString("_name")) && Tracker.WindowNewSessions < MAX_NEW_SESSIONS_IN_WINDOW)
            {
                // Writing a value triggers writing the cookie to preserve session affiation on subsequent calls.
                context.Session.SetString("_name", "waitroom");
                Tracker.WindowNewSessions += 1;
                _logger.LogInformation("New session: " + context.Session.Id);
                // Console.WriteLine("New session: " + context.Session.Id);
                _logger.LogInformation("Current new sessions: " + Tracker.WindowNewSessions);
                // Console.WriteLine("Current new sessions: " + Tracker.WindowNewSessions);
                return next();
            }
            
            if (Tracker.WindowNewSessions == MAX_NEW_SESSIONS_IN_WINDOW)
            {
                // At or exceeded quota for the new session window. Redirect to the virtual wait room
                // context.Response.WriteAsJsonAsync("test");
                Tracker.CreateSessionBlock(NEW_SESSION_BLOCK_DURATION_MINS);
                _logger.LogInformation("Session block created: " + DateTime.Now.ToLocalTime());
                // Console.WriteLine("Session block created: " + DateTime.Now.ToLocalTime());
                context.Response.Redirect(REDIRECT_URL);
                return Task.CompletedTask;
            }
            
            return next();
        }

        private void LoadSessionParams()
        {
            bool parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("NEW_SESSION_WINDOW_SECS"), out NEW_SESSION_WINDOW_SECS);
            if (!parseSuccess) throw new Exception("Cannot load value for NEW_SESSION_WINDOW_SECS");
        
            parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("MAX_NEW_SESSIONS_IN_WINDOW"), out MAX_NEW_SESSIONS_IN_WINDOW);
            if (!parseSuccess) throw new Exception("Cannot load value for MAX_NEW_SESSIONS_IN_WINDOW");

            parseSuccess = Int32.TryParse(Environment.GetEnvironmentVariable("NEW_SESSION_BLOCK_DURATION_MINS"), out NEW_SESSION_BLOCK_DURATION_MINS);
            if (!parseSuccess) throw new Exception("Cannot load value for NEW_SESSION_BLOCK_DURATION_MINS");

            REDIRECT_URL = Environment.GetEnvironmentVariable("REDIRECT_URL");
            if (String.IsNullOrEmpty(REDIRECT_URL)) throw new Exception("Cannot load value for REDIRECT_URL");
        }

    }

}
