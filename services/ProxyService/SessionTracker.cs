using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProxyService
{
    public class SessionTracker
    {
        /// <summary>
        /// The start time for a session window or a session block
        /// </summary>
        private DateTime _windowBeginTime;

        /// <summary>
        /// Determines if the tracker is currently blocking any new user connections (rate limit)
        /// </summary>
        private bool _sessionBlockActive;
        
        /// <summary>
        /// Time period for blocking any new user connections
        /// </summary>
        private readonly int _sessionBlockDurationSecs;

        /// <summary>
        /// The time period for measuring new user connections
        /// </summary>
        private readonly int _sessionWindowDurationSecs;

        /// <summary>
        /// The maximum number of new sessions in a window
        /// </summary>
        private readonly int _maxNewSessionsInWindow;

        private readonly ILogger _logger;

        /// <summary>
        /// Tracks number of new sessions in the window
        /// </summary>
        private int _counter;

        private object _countLock = new object();

        public SessionTracker(ILogger<SessionTracker> logger, IOptions<SessionTrackerOptions> options)
        {
            if (options.Value.SESSION_BLOCK_DURATION_SECS <= 0)
                throw new ApplicationException("Invalid value for SESSION_BLOCK_DURATION_SECS");

            if (options.Value.SESSION_WINDOW_DURATION_SECS <= 0)
                throw new ApplicationException("Invalid value for SESSION_WINDOW_DURATION_SECS");
            
            if (options.Value.MAX_NEW_SESSIONS_IN_WINDOW <= 0)
                throw new ApplicationException("Invalid value for MAX_NEW_SESSIONS_IN_WINDOW");

            _logger = logger;
            _counter = 0;
            _windowBeginTime = DateTime.Now;
            _sessionBlockActive = false;
            _sessionBlockDurationSecs = options.Value.SESSION_BLOCK_DURATION_SECS;
            _sessionWindowDurationSecs = options.Value.SESSION_WINDOW_DURATION_SECS;
            _maxNewSessionsInWindow = options.Value.MAX_NEW_SESSIONS_IN_WINDOW;
        }

        public bool TryAcquireSession()
        {
            RefreshNewSessionWindow();
            bool setNewSessionBlock = false;

            if (_sessionBlockActive)
                return false;

            lock (_countLock)
            {
                if (_sessionBlockActive)
                    return false;

                if (_counter >= _maxNewSessionsInWindow)
                {
                    _sessionBlockActive = true;
                    _windowBeginTime = DateTime.Now;
                    setNewSessionBlock = true;
                }
                else
                {
                    _counter++;
                }
            }

            if (setNewSessionBlock)
            {
                _logger.LogWarning("New session block starting at: {startTime}", _windowBeginTime);
                return false;
            }
            else
            {
                return true;
            }
        }

        private void RefreshNewSessionWindow()
        {
            var currentTime = DateTime.Now;
            var windowLimit = _sessionBlockActive ? _sessionBlockDurationSecs : _sessionWindowDurationSecs;

            if (currentTime.Subtract(_windowBeginTime).TotalSeconds > windowLimit)
            {
                bool sessionBlockExpried = false;
                bool newSessionWindow = false;
                
                lock (_countLock)
                {
                    // avoid multiple resets
                    windowLimit = _sessionBlockActive ? _sessionBlockDurationSecs : _sessionWindowDurationSecs;
                    if (currentTime.Subtract(_windowBeginTime).TotalSeconds > windowLimit)
                    {
                        _windowBeginTime = currentTime;

                        if (_sessionBlockActive)
                            sessionBlockExpried = true;   

                        _sessionBlockActive = false;
                        _counter = 0;
                        newSessionWindow = true;
                    }
                }

                if (sessionBlockExpried)
                    _logger.LogWarning("Session block expired.");    

                if (newSessionWindow)
                    _logger.LogInformation("New session window starting at: {startTime} ", _windowBeginTime.ToLocalTime());
            }
        }

    }
}