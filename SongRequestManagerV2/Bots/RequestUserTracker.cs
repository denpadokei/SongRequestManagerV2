using System;

namespace SongRequestManagerV2
{
    public class RequestUserTracker
    {
        public int numRequests = 0;
        public DateTime resetTime = DateTime.Now;
    }
}
