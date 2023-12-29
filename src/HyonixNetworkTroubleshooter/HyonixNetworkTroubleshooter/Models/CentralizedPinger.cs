using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HyonixNetworkTroubleshooter.Models
{
    public class CentralizedPinger
    {
        private int _timeout;
        private int _pingCountLimit;
        private int _timeoutThreshold;
        private static readonly byte[] Buffer = new byte[32];
        private readonly TimeSpan _pingDuration;
        private ConcurrentDictionary<(IPAddress, int), PingStatistics> _pingResults = new ConcurrentDictionary<(IPAddress, int), PingStatistics>();
        private ConcurrentDictionary<(IPAddress, int), Task> _pingingTasks = new ConcurrentDictionary<(IPAddress, int), Task>();
        private CancellationToken _ct;
        private Task _pingingTask;

        public CentralizedPinger(TimeSpan pingDuration, int timeout, int pingCountLimit, int timeoutThreshold, CancellationToken ct)
        {
            _pingDuration = pingDuration;
            _timeout = timeout;
            _pingCountLimit = pingCountLimit;
            _timeoutThreshold = timeoutThreshold;
            _ct = ct ;
            for(int i = 0; i< Buffer.Length; i++)
                Buffer[i] = (byte)(32 + i);
        }

        public void RegisterRoute(IPAddress ipAddress, int ttl)
        {
            var key = (ipAddress, ttl);
            if (!_pingingTasks.ContainsKey(key))
            {
                var pingTask = PingIPAsync(ipAddress, ttl, _pingDuration, _ct);
                _pingingTasks.TryAdd(key, pingTask);
            }
        }



        public async Task<PingStatistics> GetPingResultAsync(IPAddress ipAddress, int ttl)
        {
            var key = (ipAddress, ttl);
            if (ipAddress == null || ipAddress.Equals(IPAddress.Any))
                return new PingStatistics();

            while (!_pingResults.ContainsKey(key))
            {
                await Task.Delay(100);
            }

            return _pingResults[key];
        }

        private async Task PingIPAsync(IPAddress ip, int ttl, TimeSpan pingDuration, CancellationToken cancellationToken)
        {
            var statistics = new PingStatistics();
            var key = (ip, ttl);
            var options = new PingOptions(ttl, false);
            int timeoutCount = 0;
            int pingCount = 0;

            using (var pinger = new Ping())
            {
                var endTime = DateTime.UtcNow.Add(pingDuration);
                while (DateTime.UtcNow < endTime && pingCount < _pingCountLimit)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    if (ip == null || ip.Equals(IPAddress.Any))
                        break;

                    try
                    {
                        var startTime = DateTime.UtcNow;
                        var reply = await pinger.SendPingAsync(ip, _timeout, Buffer, options);
                        var elapsed = DateTime.UtcNow - startTime;

                        UpdateStatistics(reply, statistics, elapsed.TotalMilliseconds);
                        pingCount++;

                        if (reply.Status != IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                        {
                            timeoutCount++;
                            if (timeoutCount >= _timeoutThreshold && DateTime.UtcNow >= endTime)
                            {
                                break;
                            }
                        }

                    }
                    catch (PingException ex)
                    {
                        // Log the exception details
                        Console.WriteLine(ex);
                        timeoutCount++;
                        if (timeoutCount >= _timeoutThreshold)
                        {
                            // Switch to duration-based pinging
                            break;
                        }
                    }
                }
            }
            _pingResults[key] = statistics; // Use indexer for thread safety
        }

    

    private void UpdateStatistics(PingReply reply, PingStatistics statistics, double elapsed)
        {
            lock (statistics)
            {
                double bestTime = statistics.BestTime > 0 ? statistics.BestTime : double.MaxValue;
                // Thread-safe updates to PingStatistics
                if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                {
                    statistics.Sent++;
                    statistics.Received++;
                    double lastTime = reply.Status == IPStatus.Success ? reply.RoundtripTime : elapsed;
                    statistics.LastTime = lastTime;
                    statistics.BestTime = Math.Min(bestTime, lastTime);
                    statistics.WorstTime = Math.Max(statistics.WorstTime, lastTime);
                    // Update the average time
                    statistics.AverageTime = ((statistics.AverageTime * (statistics.Received - 1)) + lastTime) / statistics.Received;
                }
                else
                {
                    statistics.Sent++;
                }
            }
        }

        public void Reset()
        {

            // Wait for all pinging tasks to complete
            Task.WhenAll(_pingingTasks.Values).Wait();

            _pingResults.Clear();
            _pingingTasks.Clear();


        }
    }


}


