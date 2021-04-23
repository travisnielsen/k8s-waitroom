namespace ProxyService
{
    public class RateLimitMiddlewareOptions
    {
        public string HTML_FILENAME { get; set; }
        public int WAITROOM_RESPONSE_CODE { get; set; }
        public bool WAITROOM_ENABLED { get; set; }
        public string TRACKING_COOKIE { get; set; }
    }
}