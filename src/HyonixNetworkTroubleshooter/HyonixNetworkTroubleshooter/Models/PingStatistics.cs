namespace HyonixNetworkTroubleshooter.Models
{
    public class PingStatistics
    {
        public int Sent { get; set; }
        public int Received { get; set; }
        public double BestTime { get; set; }
        public double AverageTime { get; set; }
        public double WorstTime { get; set; }
        public double LastTime { get; set; }
        public double LossPercentage => Sent > 0 ? ((Sent - Received) / (double)Sent) * 100 : 0;

        public override string ToString()
        {
            return $"{nameof(Sent)}: {Sent}," +
                $"{nameof(Received)}: {Received}," +
                $"{nameof(LossPercentage)}: {LossPercentage}," +
                $"{nameof(BestTime)}: {BestTime}," +
                $"{nameof(AverageTime)}: {AverageTime}," +
                $"{nameof(WorstTime)}: {WorstTime}," +
                $"{nameof(LastTime)}: {LastTime}";
        }
    }


}


