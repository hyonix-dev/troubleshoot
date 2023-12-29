
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace HyonixNetworkTroubleshooter.Helpers
{
    public class CsvDownloader
    {
        public static async Task<Dictionary<string, string>> DownloadCsvAsync(string url)
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    // Download the CSV content
                    var csvString = await httpClient.GetStringAsync(url);

                    return ParseCsv(csvString);
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        private static Dictionary<string, string> ParseCsv(string csvContent)
        {
            var result = new Dictionary<string, string>();
            using (var reader = new StringReader(csvContent))
            {
                string line;
                bool firstLine = true;

                while ((line = reader.ReadLine()) != null)
                {
                    // Skip the header line
                    if (firstLine)
                    {
                        firstLine = false;
                        continue;
                    }

                    var parts = line.Split(',');
                    if (parts.Length >= 2)
                    {
                        result[parts[0]] = parts[1];
                    }
                }
            }

            return result;
        }
    }
}
