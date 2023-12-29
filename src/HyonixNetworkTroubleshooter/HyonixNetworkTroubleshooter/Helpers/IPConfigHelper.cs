using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace HyonixNetworkTroubleshooter.Helpers
{
    public class IPConfigHelper
    {
        public static string GetPublicIPAddress(string url = "http://ifconfig.me/ip")
        {
            using (var client = new WebClient())
            {
                try
                {
                    string response = client.DownloadString(url);
                    return response.Trim();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error occurred: " + e.Message);
                    return null;
                }
            }
        }

        public static string GetIpConfigOutput()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("ipconfig", "/all")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    return result;
                }
            }
        }
    }
}
