using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ApplicationInsights;

namespace ProxyService
{
    public class RateLimitMiddleware
    {
        private RequestDelegate _next; 
        private ILogger _logger; 
        private ITempDataProvider _cookieProvider; 
        private SessionTracker _tracker;
        private byte[] _html;
        private int _htmlResponseCode;
        private bool _waitRoomEnabled;
        private string _trackingCookie;
        private readonly TelemetryClient _telemetry;

        public RateLimitMiddleware(RequestDelegate next, SessionTracker tracker, ILogger<RateLimitMiddleware> logger, IOptions<RateLimitMiddlewareOptions> options,
            ITempDataProvider cookieProvider, TelemetryClient telemetry)
        {
            _next = next;
            _logger = logger;
            _cookieProvider = cookieProvider;
            _tracker = tracker;
            _html = LoadHtml(options.Value.HTML_FILENAME);
            _telemetry = telemetry;

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
            var cookieData = _cookieProvider.LoadTempData(context);
            bool hasWaitRoomId = cookieData.ContainsKey("_waitroom");
            object proxyUserId;
            bool hasUserId = cookieData.TryGetValue("_proxyUserId", out proxyUserId);

            // Any connection with a session ID is allowed
            if (cookieData.ContainsKey("_proxySession"))
            {
                if (hasUserId)
                    _telemetry.Context.User.Id = cookieData["_proxyUserId"].ToString();
                
                return _next(context);
            }

            // Get or create a user ID for tracking context
            if (! hasUserId)
            {
                string trackingCookieSessionId = context.Request.Cookies[_trackingCookie];
                proxyUserId = String.IsNullOrEmpty(trackingCookieSessionId) ? Guid.NewGuid().ToString() : trackingCookieSessionId;
                cookieData.Add("_proxyUserId", proxyUserId);
            }

            _telemetry.Context.User.Id = proxyUserId.ToString();
            _telemetry.Context.Session.Id = proxyUserId.ToString();

            if (! _tracker.TryAcquireSession())
            {
                // This is a new user connecting during an active session block

                if (_waitRoomEnabled)
                {
                    if (! hasWaitRoomId)
                    {
                        string waitRoomId = Guid.NewGuid().ToString();
                        cookieData.Add("_waitroom", waitRoomId);
                        _cookieProvider.SaveTempData(context, cookieData);
                        // _logger.LogWarning("User {id} in wait room", proxyUserId);
                        _telemetry.Context.Operation.ParentId = waitRoomId;
                        _telemetry.TrackEvent("waitroom start", new Dictionary<string, string> { { "waitroom_id", waitRoomId } } );
                    }
                    else
                    {
                        // TODO: Check for context.Session.GetInt("_retries"); Increment this and then do something different in the response - could do some templating here

                        // Track page refresh in telemetry
                        string waitRoomId = cookieData["_waitroom"].ToString();
                        _telemetry.Context.Operation.ParentId = waitRoomId;
                    }
                    
                    context.Response.ContentLength = _html.Length;
                    context.Response.ContentType = "text/html";
                    context.Response.StatusCode = _htmlResponseCode;
                    return context.Response.Body.WriteAsync(_html).AsTask();
                }
                else
                {
                    _logger.LogWarning("User {id} exceeded quota (waitroom disabled)", proxyUserId);
                    return _next(context);
                }
            }

            // Clear waitroom data from the proxy cookie
            if (hasWaitRoomId)
            {
                string waitRoomId = cookieData["_waitroom"].ToString();
                _telemetry.Context.Operation.ParentId = waitRoomId;
                _telemetry.TrackEvent("waitroom end", new Dictionary<string, string> { { "waitroom_id", waitRoomId } }  );
                cookieData.Remove("_waitroom");
                _cookieProvider.SaveTempData(context, cookieData);
            }
            
            cookieData.Add("_proxySession", "true");
            _cookieProvider.SaveTempData(context, cookieData);
            return _next(context);
        }
    }

}