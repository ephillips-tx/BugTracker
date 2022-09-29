namespace BugTracker.Models.ChartModels
{
    sealed public class PlotlyBarData
    {
        public List<PlotlyBar> Data { get; set; }
    }


    sealed public class PlotlyBar
    {
        public string[] X { get; set; }
        public int[] Y { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
