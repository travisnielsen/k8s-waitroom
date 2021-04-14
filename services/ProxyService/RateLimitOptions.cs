

namespace ProxyService
{
    public class RateLimitOptions
    {
        public string HTML_FILENAME { get; set; }
        public int NEW_SESSION_WINDOW_SECS { get; set; }
        public int MAX_NEW_SESSIONS_IN_WINDOW { get; set; }
        public int NEW_SESSION_BLOCK_DURATION_MINS { get; set; }
    }
}