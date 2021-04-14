using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProxyService
{
    public class RateLimitMiddleware
    {
        private RequestDelegate _next; 
        private ILogger _logger; 
        private SessionTracker _tracker;
        private RateLimitOptions _options;
        private byte[] _html;


        public RateLimitMiddleware(RequestDelegate next, SessionTracker tracker, ILogger<RateLimitMiddleware> logger, IOptions<RateLimitOptions> options)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
            _tracker = tracker;
            _html = LoadHtml();

            // TODO: Check to make sure options are correclty populated. If not, throw an ApplicationException to halt execution
        }

        private byte[] LoadHtml()
        {
            byte[] html = null;

            try
            {
                html = File.ReadAllBytes(_options.HTML_FILENAME);
            }
            catch (Exception e)
            {
                throw new FileNotFoundException("Could not load html file. Check HTML_FILENAME environment variable and make sure file is in the correct location", e.InnerException);
            }

            return html;
        }

        public Task Invoke(HttpContext context)
        {
            // Any connection with a valid session cookie is allowed
            if (!string.IsNullOrEmpty(context.Session.GetString("_name")))
            {
                _logger.LogInformation("Existing session: " + context.Session.Id);
                return _next(context);
            }

            // Remove session block if its expired
            if (_tracker.SessionBlockActive && _tracker.SessionBlockIsExpired())
            {
                _tracker.SessionBlockActive = false;
                _logger.LogInformation("Session block removed: " + DateTime.Now.ToLocalTime());
            }

            // Deny new users during an active session block
            if (_tracker.SessionBlockActive && !_tracker.SessionBlockIsExpired())
            {
                if (context.Session.GetString("_name") == null)
                {
                    // This is a new user connecting during an active session block
                    context.Response.ContentLength = _html.Length;
                    context.Response.ContentType = "text/html";
                    context.Response.StatusCode = _options.WAITROOM_RESPONSE_CODE;
                    return context.Response.Body.WriteAsync(_html).AsTask();
                }
            }
            
            // Create new session window if current one is expired
            _tracker.RefreshNewSessionWindow(_options.NEW_SESSION_WINDOW_SECS);

            // Check if the user has any waitroom session data. Empty / default sessions are pased into HttpContext on each new call.
            // If we are under quota and no existing session cookie exists. Create one.
            if (string.IsNullOrEmpty(context.Session.GetString("_name")) && _tracker.WindowNewSessions < _options.MAX_NEW_SESSIONS_IN_WINDOW)
            {
                // Writing a value triggers writing the cookie to preserve session affiation on subsequent calls.
                context.Session.SetString("_name", "waitroom");
                _tracker.WindowNewSessions += 1;
                _logger.LogInformation("New session: " + context.Session.Id);
                _logger.LogInformation("Current new sessions: " + _tracker.WindowNewSessions);
                return _next(context);
            }
            
            if (_tracker.WindowNewSessions == _options.MAX_NEW_SESSIONS_IN_WINDOW)
            {
                // At or exceeded quota for the new session window. Redirect to the virtual wait room
                _tracker.CreateSessionBlock(_options.NEW_SESSION_BLOCK_DURATION_MINS);
                _logger.LogInformation("Session block created: " + DateTime.Now.ToLocalTime());

                context.Response.ContentLength = _html.Length;
                context.Response.ContentType = "text/html";
                context.Response.StatusCode = _options.WAITROOM_RESPONSE_CODE;
                return context.Response.Body.WriteAsync(_html).AsTask();
            }
            
            return _next(context);
        }




    }

}