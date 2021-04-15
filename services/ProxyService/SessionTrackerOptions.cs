namespace ProxyService
{
    public class SessionTrackerOptions
    {
        public int SESSION_WINDOW_DURATION_SECS { get; set; }
        public int SESSION_BLOCK_DURATION_SECS { get; set; }
        public int MAX_NEW_SESSIONS_IN_WINDOW { get; set; }
    }
}