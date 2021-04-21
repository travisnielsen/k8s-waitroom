using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProxyService
{
    public class RateLimitMiddleware
    {
        private RequestDelegate _next; 
        private ILogger _logger; 
        private SessionTracker _tracker;
        private byte[] _html;
        private int _htmlResponseCode;


        public RateLimitMiddleware(RequestDelegate next, SessionTracker tracker, ILogger<RateLimitMiddleware> logger, IOptions<RateLimitMiddlewareOptions> options)
        {
            _next = next;
            _logger = logger;
            _tracker = tracker;
            _html = LoadHtml(options.Value.HTML_FILENAME);
            _htmlResponseCode = options.Value.WAITROOM_RESPONSE_CODE;
        }

        private byte[] LoadHtml(string fileName)
        {
            byte[] html = null;

            try
            {
                html = File.ReadAllBytes(fileName);
            }
            catch (Exception e)
            {
                throw new FileNotFoundException("Could not load html file. Check HTML_FILENAME environment variable and make sure file is in the correct location", e.InnerException);
            }

            return html;
        }

        public Task Invoke(HttpContext context, ITempDataProvider cookieData)
        {
            // Proxy all traffic to the backend if rate limiting is disabled
            if (System.Environment.GetEnvironmentVariable("RATE_LIMIT_ENABLED").ToLower() == "false")
                return _next(context);

            // Any connection with a valid session cookie is allowed
            if (cookieData.LoadTempData(context).ContainsKey("_currentUser"))
            {
                return _next(context);
            }

            /*
            if (!string.IsNullOrEmpty(context.Session.GetString("_name")))
            {
                // _logger.LogInformation("Existing session: " + context.Session.Id);
                return _next(context);
            }
            */

            if (! _tracker.TryAcquireSession())
            {
                // This is a new user connecting during an active session block
                // TODO: Check for context.Session.GetInt("_retries"); Increment this and then do something different in the response - could do some templating here
                context.Response.ContentLength = _html.Length;
                context.Response.ContentType = "text/html";
                context.Response.StatusCode = _htmlResponseCode;
                return context.Response.Body.WriteAsync(_html).AsTask();
            }
            
            // Writing a value triggers writing the cookie to preserve session affiation on subsequent calls.
            // context.Session.SetString("_name", "waitroom");
            IDictionary<string, object> data = new Dictionary<string, object>();
            data.Add("_currentUser", Guid.NewGuid().ToString());
            cookieData.SaveTempData(context, data);
            return _next(context);
        }
    }

}