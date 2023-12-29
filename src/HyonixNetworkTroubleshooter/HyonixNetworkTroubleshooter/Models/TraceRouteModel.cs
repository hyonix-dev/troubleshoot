
using HyonixNetworkTroubleshooter.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace HyonixNetworkTroubleshooter.Models
{
    public class TraceRouteModel
    {
        List<TraceRouter> TraceRouters = new List<TraceRouter>();
        private  TimeSpan _durationOfPingSampling ;
        private int _pingTimeout;
        private int _pingCountLimit;
        private int _pingTimeoutCountThreshold ;
        private readonly int totalWork = 10000;
        private readonly int tracingProgressShare = 1000;
        private IProgress<int> _progress;
        ProgressTracker _progressTracker;
        CentralizedPinger _centralizedPinger;

#if DEBUG
        private string _hostNamesUriString = "http://localhost:8000/mtr-config.xml";
#else
        private string _hostNamesUriString = "https://troubleshoot.hyonix.com/mtr-config.xml";
#endif
        public string PublicIPUrl { get; private set; }
        private Dictionary<string, string> _hostNames;
        CancellationToken _ct;

        public Dictionary<string, string> HostNames
        {
            get
            {
                return _hostNames ?? throw new Exception("No internet connection!");
            }
        }
        public TraceRouteModel(IProgress<int> progress, CancellationToken cancellationToken)
        {
            _ct = cancellationToken;
            _progress = progress;
            _progressTracker = new ProgressTracker(totalWork, _progress);
            

        }
        public async Task Initialize()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetStringAsync(_hostNamesUriString);
                    ParseXml(response);
                }

                _centralizedPinger = new CentralizedPinger(_durationOfPingSampling, _pingTimeout, _pingCountLimit, _pingTimeoutCountThreshold, _ct);

                foreach (var hostName in HostNames)
                    TraceRouters.Add(new TraceRouter(_centralizedPinger, hostName.Value, tracingProgressShare / HostNames.Count, _progressTracker));
            }
            catch (Exception ex)
            {
                throw new Exception("Not connected to the internet!");
            }
        }

        private void ParseXml(string xmlData)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlData);

            var serversNode = doc.DocumentElement.SelectSingleNode("/Configuration/Servers");
            _hostNames = new Dictionary<string, string>();
            foreach (XmlNode serverNode in serversNode.ChildNodes)
            {
                var name = serverNode.Attributes["name"].Value;
                var hostname = serverNode.Attributes["hostname"].Value;
                _hostNames[name] = hostname;
            }

            var settingsNode = doc.DocumentElement.SelectSingleNode("/Configuration/Settings");
            PublicIPUrl = settingsNode["PublicIPUrl"].InnerText;
            _durationOfPingSampling = TimeSpan.FromSeconds(int.Parse(settingsNode["PingSamplingDuration"].InnerText));
            _pingTimeout = int.Parse(settingsNode["PingTimeOut"].InnerText);
            _pingCountLimit = int.Parse(settingsNode["PingCountLimit"].InnerText);
            _pingTimeoutCountThreshold = int.Parse(settingsNode["PingTimeoutCountThreshold"].InnerText);
        }
        public async Task<IDictionary<string, IEnumerable<TraceRouteResult>>> Execute(CancellationToken cancellationToken)
        {
            _progress.Report(0);
            _centralizedPinger.Reset();


            var traceRoutingResults = new ConcurrentDictionary<string, IEnumerable<TraceRouteResult>>();
            var traceRouteTasks = new List<Task>();


            traceRouteTasks.Add(Task.Run(async () =>
            {
                int delayMilliseconds = 100;
                TimeSpan pingingTimeEstimation = _durationOfPingSampling + _durationOfPingSampling;
                var endTime = DateTime.UtcNow.Add(pingingTimeEstimation);
                while (DateTime.UtcNow < endTime)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    _progressTracker.ReportProgress((totalWork - tracingProgressShare) / (int)(pingingTimeEstimation.TotalMilliseconds / delayMilliseconds));
                    await Task.Delay(delayMilliseconds);
                }
            }));


            foreach (TraceRouter traceRouter in TraceRouters)
            {
                traceRouteTasks.Add(Task.Run(async () =>
                {
                    var traceRoutes = await traceRouter.GetTraceRouteAsync(cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                        return;

                    foreach (var route in traceRoutes)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        route.Statistics = await _centralizedPinger.GetPingResultAsync(route.RouteNumber == 1 ? route.IPAddress : traceRouter.HostAddress, route.RouteNumber);
                    }
                    //await traceRouter.ContinuousPingAsync(traceRoutes, _numberOfPingSamples, cancellationToken, _progressTracker);
                    traceRoutingResults[traceRouter.HostNameOrAddress] = traceRoutes;
                }
                ));
            }

            await Task.WhenAll(traceRouteTasks);
            if (cancellationToken.IsCancellationRequested)
                return null;
            _progress.Report(100);
            return traceRoutingResults;
        }

    }
}
