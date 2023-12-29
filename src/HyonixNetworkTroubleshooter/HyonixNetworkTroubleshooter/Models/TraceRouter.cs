using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HyonixNetworkTroubleshooter.Models
{



    public class TraceRouter
    {
        private const string Data = "aaaa";
        private const int Timeout = 1000;
        private static readonly byte[] Buffer = Encoding.ASCII.GetBytes(Data);
        private readonly CentralizedPinger _pinger;
        private readonly string _hostNameOrAddress;
        public string HostNameOrAddress => _hostNameOrAddress;
        public IPAddress HostAddress { get; private set; }
        private int _progressShare; // Share of progress allocated to tracing
        private readonly ProgressTracker _progressTracker;
        private int _pingCompletedProgress = 0;

        public TraceRouter(CentralizedPinger pinger, string hostNameOrAddress, int progressShare, ProgressTracker progressTracker)
        {
            _pinger = pinger;
            _hostNameOrAddress = hostNameOrAddress;
            _progressShare = progressShare;
            _progressTracker = progressTracker;
        }



        public async Task<IEnumerable<TraceRouteResult>> GetTraceRouteAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task<TraceRouteResult>>();
            const int maxHops = 30;
            const int maxAttempts = 20; // Max attempts per hop

            IPAddress destinationIP = await ResolveHostNameToIP(_hostNameOrAddress);
            HostAddress = destinationIP;

            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                int currentTTL = ttl; // Capture the current TTL in the loop
                var task = Task.Run(async () =>
                {
                    for (int attempt = 0; attempt < maxAttempts; attempt++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return null;

                        var result = await PerformPingAsync(_hostNameOrAddress, currentTTL, cancellationToken);

                        if (result != null && result.IPAddress != IPAddress.Any)
                        {
                            result.RouteNumber = currentTTL;
                            return result; // Valid response received
                        }

                        // Optionally, add a delay here if you want to space out retries
                    }

                    return new TraceRouteResult
                    {
                        IPAddress = IPAddress.Any,
                        RouteNumber = currentTTL,
                        HostName = "No response after retries",
                        Statistics = new PingStatistics()
                    };
                });

                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            if (cancellationToken.IsCancellationRequested)
                return null;

            // Filter out null results (if cancellation was requested)
            var validResults = RemoveRepetitiveMembers(results.Where(r => r != null).ToList());

            foreach (var route in validResults)
            {
                if (route.RouteNumber == 1)
                    _pinger.RegisterRoute(route.IPAddress, route.RouteNumber);
                else
                    _pinger.RegisterRoute(destinationIP, route.RouteNumber);
            }
            // Update progress after all tasks are complete

            _progressTracker.ReportProgress(_progressShare);
            return validResults;
        }
        private IEnumerable<TraceRouteResult> RemoveRepetitiveMembers(IEnumerable<TraceRouteResult> traceRouteResults)
        {
            var resultsList = traceRouteResults.OrderBy(x => x.RouteNumber).ToList(); // Convert to List for easy manipulation

            while (resultsList.Count > 1) // Ensure there are at least two elements to compare
            {
                var lastElementIP = resultsList.Last().IPAddress;
                // Check if the last IP exists elsewhere in the list
                bool isRepetitive = resultsList.Take(resultsList.Count - 1).Any(result => result.IPAddress.Equals(lastElementIP));

                if (isRepetitive)
                {
                    // Remove the last element if it's repetitive
                    resultsList.RemoveAt(resultsList.Count - 1);
                }
                else
                {
                    break; // Stop if the last IP is not repetitive
                }
            }

            return resultsList;
        }


        private static async Task<TraceRouteResult> PerformPingAsync(string address, int ttl, CancellationToken cancellationToken)
        {
            using (var pinger = new Ping())
            {
                var pingerOptions = new PingOptions(ttl, true);
                var reply = await pinger.SendPingAsync(address, Timeout, Buffer, pingerOptions);

                if (cancellationToken.IsCancellationRequested)
                    return null;

                var result = new TraceRouteResult
                {
                    IPAddress = reply.Address ?? IPAddress.Any,
                    Statistics = new PingStatistics(),
                    HostName = "Resolving..." // Temporary placeholder
                };

                if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                {
                    if (reply.Address != null)
                    {
                        ResolveHostNameInBackground(result, reply.Address);
                    }
                    else
                    {
                        result.HostName = "No response from host";
                        return null;
                    }

                    return result;
                }

                return null;
            }
        }
        private static void ResolveHostNameInBackground(TraceRouteResult result, IPAddress ipAddress)
        {
            Task.Run(async () =>
            {
                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                    result.HostName = hostEntry.HostName;
                }
                catch
                {
                    Console.WriteLine(ipAddress.ToString());
                    result.HostName = "Unknown";
                }
            });
        }

        private async Task<IPAddress> ResolveHostNameToIP(string hostName)
        {
            try
            {
                // Check if the input is already a valid IP address
                if (IPAddress.TryParse(hostName, out var ipAddress))
                {
                    return ipAddress;
                }

                // Resolve the hostname to an IP address
                var hostEntry = await Dns.GetHostEntryAsync(hostName);

                // Optionally handle multiple addresses or select an address based on criteria
                // For simplicity, taking the first address
                return hostEntry.AddressList.FirstOrDefault();
            }
            catch (Exception ex) // Consider catching specific exceptions
            {
                // Handle or log the exception as appropriate
                // For example, log the exception message: Console.WriteLine(ex.Message);
                return null;
            }
        }
    }


}


