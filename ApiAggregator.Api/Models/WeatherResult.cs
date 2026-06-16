namespace ApiAggregator.Api.Models
{
    public class WeatherResult
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Time { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double WindSpeed { get; set; }
        public string TemperatureUnit { get; set; } = "°C";
        public string WindSpeedUnit { get; set; } = "km/h";
    }
}
