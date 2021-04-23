using System;
using System.Collections.Generic;
using System.IO;
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
        private ITempDataProvider _cookieData; 
        private SessionTracker _tracker;
        private byte[] _html;
        private int _htmlResponseCode;
        private bool _waitRoomEnabled;
        private string _trackingCookie;

        public RateLimitMiddleware(RequestDelegate next, SessionTracker tracker, ILogger<RateLimitMiddleware> logger, IOptions<RateLimitMiddlewareOptions> options, ITempDataProvider cookieData)
        {
            _next = next;
            _logger = logger;
            _cookieData = cookieData;
            _tracker = tracker;
            _html = LoadHtml(options.Value.HTML_FILENAME);

            // Check values and set defaults if missing
            _waitRoomEnabled = options.Value.WAITROOM_ENABLED; // Runtime error at startup if setting does not exist or is invalid
            _htmlResponseCode = options.Value.WAITROOM_RESPONSE_CODE == 0 ? 429 : options.Value.WAITROOM_RESPONSE_CODE;
            _trackingCookie = string.IsNullOrEmpty(options.Value.TRACKING_COOKIE) ? "" : options.Value.TRACKING_COOKIE;
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

        public Task Invoke(HttpContext context)
        {
            // Any connection with a valid session cookie is allowed
            if (_cookieData.LoadTempData(context).ContainsKey("_proxySessionId"))
            {
                return _next(context);
            }

            var proxySessionId = context.Request.Cookies[_trackingCookie];

            if (string.IsNullOrEmpty(proxySessionId))
                proxySessionId = Guid.NewGuid().ToString();

            if (! _tracker.TryAcquireSession())
            {
                // This is a new user connecting during an active session block
                // TODO: Check for context.Session.GetInt("_retries"); Increment this and then do something different in the response - could do some templating here

                if (_waitRoomEnabled)
                {
                    context.Response.ContentLength = _html.Length;
                    context.Response.ContentType = "text/html";
                    context.Response.StatusCode = _htmlResponseCode;
                    _logger.LogWarning("User {id} in wait room", proxySessionId);
                    return context.Response.Body.WriteAsync(_html).AsTask();
                }
                else
                {
                    _logger.LogWarning("User {id} exceeded quota (waitroom disabled)", proxySessionId);
                    return _next(context);
                }

            }
            
            // Writing a value triggers writing the cookie to preserve session affiation on subsequent calls.
            IDictionary<string, object> data = new Dictionary<string, object>();
            data.Add("_proxySessionId", proxySessionId);
            _cookieData.SaveTempData(context, data);
            return _next(context);
        }
    }

}