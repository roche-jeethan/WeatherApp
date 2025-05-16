using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WeatherApp.Models
{
    public class WeatherInfo
    {
        public Main Main { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
    }

    public class Main
    {
        public float Temp { get; set; }
        public int Humidity { get; set; }
    }
}