namespace BugTracker.Models.ChartModels
{
    sealed public class AmChartData
    {
        public AmItem[] Data { get; set; }
    }


    sealed public class AmItem
    {
        public string Project { get; set; }
        public int Tickets { get; set; }
        public int Developers { get; set; }
    }
}
