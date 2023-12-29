using System.Net;

namespace HyonixNetworkTroubleshooter.Models
{
    public class TraceRouteResult
    {
        public int RouteNumber { get; set; }
        public IPAddress IPAddress { get; set; }
        public string HostName { get; set; }
        public PingStatistics Statistics { get; set; } = new PingStatistics();
    }


}


