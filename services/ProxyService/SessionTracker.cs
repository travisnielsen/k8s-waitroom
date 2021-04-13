using System;
using Microsoft.Extensions.Logging;

namespace ProxyService
{
    public class SessionTracker
    {
        public DateTime WindowBeginTime { get; set; }
        public int WindowNewSessions { get; set; }
        public bool SessionBlockActive { get; set; }
        private DateTime SessionBlockStartTime { get; set; }
        private int SessionBlockDurationMins { get; set; }
        private readonly ILogger _logger;

        public SessionTracker(ILogger<SessionTracker> logger)
        {
            _logger = logger;
            WindowBeginTime = DateTime.Now;
            WindowNewSessions = 0;
            SessionBlockActive = false;
        }

        public void CreateSessionBlock(int sessionBlockDurationMins)
        {
            SessionBlockActive = true;
            SessionBlockDurationMins = sessionBlockDurationMins;
            SessionBlockStartTime = DateTime.Now;
        }

        public bool SessionBlockIsExpired()
        {
            if (DateTime.Now.Subtract(this.SessionBlockStartTime).TotalMinutes >= this.SessionBlockDurationMins)
                return true;
            
            return false;
        }

        public void RefreshNewSessionWindow(int newSessionWindowSecs)
        {
            if (DateTime.Now.Subtract(WindowBeginTime).TotalSeconds > newSessionWindowSecs)
            {
                WindowBeginTime = DateTime.Now;
                WindowNewSessions = 0;
                _logger.LogInformation("New session window starting at: " + WindowBeginTime.ToLocalTime());

            }
        }
    }
}