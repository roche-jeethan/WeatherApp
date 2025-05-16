using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using DotNetEnv;
using WeatherApp.Models;
using Avalonia.Threading; // Add this using statement

namespace WeatherApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private readonly HttpClient _client;
        private string _cityName = "";
        private string _temperature = "--";
        private string _humidity = "--";
        private string _geminiAdvice = "Enter a city name to get weather information";
        private bool _isLoading;
        private string _errorMessage = string.Empty;

        // Properties with ReactiveUI notifications
        public string CityName
        {
            get => _cityName;
            set => this.RaiseAndSetIfChanged(ref _cityName, value);
        }

        public string Temperature
        {
            get => _temperature;
            private set => this.RaiseAndSetIfChanged(ref _temperature, value);
        }

        public string Humidity
        {
            get => _humidity;
            private set => this.RaiseAndSetIfChanged(ref _humidity, value);
        }

        public string GeminiAdvice
        {
            get => _geminiAdvice;
            private set => this.RaiseAndSetIfChanged(ref _geminiAdvice, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        // New properties for additional weather information
        private string _description = "--";
        private string _tempRange = "--";
        private string _wind = "--";
        private string _sunTimes = "--";
        private string _pressure = "--";

        public string Description
        {
            get => _description;
            private set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public string TempRange
        {
            get => _tempRange;
            private set => this.RaiseAndSetIfChanged(ref _tempRange, value);
        }

        public string Wind
        {
            get => _wind;
            private set => this.RaiseAndSetIfChanged(ref _wind, value);
        }

        public string SunTimes
        {
            get => _sunTimes;
            private set => this.RaiseAndSetIfChanged(ref _sunTimes, value);
        }

        public string Pressure
        {
            get => _pressure;
            private set => this.RaiseAndSetIfChanged(ref _pressure, value);
        }

        // Add these new fields
        private string _location = "--";

        // Add this new property
        public string Location
        {
            get => _location;
            private set => this.RaiseAndSetIfChanged(ref _location, value);
        }

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand RefreshCommand { get; }

        public MainWindowViewModel()
        {
            _client = new HttpClient();
            Env.Load();

            // Initialize commands correctly
            var canExecute = this.WhenAnyValue(x => x.CityName,
                cityName => !string.IsNullOrEmpty(cityName));

            SearchCommand = ReactiveCommand.CreateFromTask(SearchWeatherAsync, canExecute);
            RefreshCommand = ReactiveCommand.CreateFromTask(SearchWeatherAsync, canExecute);
        }

        private async Task SearchWeatherAsync()
        {
            if (string.IsNullOrWhiteSpace(CityName))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    ErrorMessage = "Please enter a city name");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = true;
                ErrorMessage = "";
            });

            try
            {
                var weatherApiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
                var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

                string weatherUrl = $"https://api.openweathermap.org/data/2.5/weather?q={CityName}&appid={weatherApiKey}&units=metric";
                string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={geminiApiKey}";

                // Get Weather Data
                var response = await _client.GetAsync(weatherUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ErrorMessage = "Could not fetch weather data. Please check the city name.";
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var weather = JsonSerializer.Deserialize<WeatherInfo>(json, options);
                Console.WriteLine($"Weather Data: \n {json}");
                if (weather?.Main == null || weather.Weather.Count == 0)
                {
                    throw new Exception("Invalid weather data received");
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Location = $"{weather.Name}, {weather.Sys.Country}";
                    Temperature = $"{weather.Main.Temp:F1}°C";
                    Humidity = $"Humidity: {weather.Main.Humidity}%";
                    TempRange = $"Min: {weather.Main.TempMin:F1}°C • Max: {weather.Main.TempMax:F1}°C";
                    Description = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(weather.Weather[0].Description);
                    Wind = $"Wind: {weather.Wind.Speed:F1} m/s";
                    Pressure = $"Pressure: {weather.Main.Pressure} hPa";

                    // Convert Unix timestamp to local time for sunrise/sunset
                    var sunrise = DateTimeOffset.FromUnixTimeSeconds(weather.Sys.Sunrise).LocalDateTime;
                    var sunset = DateTimeOffset.FromUnixTimeSeconds(weather.Sys.Sunset).LocalDateTime;
                    SunTimes = $"🌅 {sunrise:HH:mm} • 🌇 {sunset:HH:mm}";
                });

                // Get Gemini Response
                var geminiRequest = new GeminiRequest
                {
                    Contents = new List<GeminiContent>
                    {
                        new GeminiContent
                        {
                            Parts = new List<Part>
                            {
                                new Part
                                {
                                    Text = $"Give greeting message, along with what's the weather like and friendly short one-liner advice that enhances the quality of the day based on this weather:\nTemperature: {weather.Main.Temp}°C, Humidity: {weather.Main.Humidity}%"
                                }
                            }
                        }
                    },
                    GenerationConfig = new GenerationConfig
                    {
                        MaxOutputTokens = 65,
                        Temperature = 0.7f
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(geminiRequest);
                var content = new StringContent(jsonRequest, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                var geminiResponse = await _client.PostAsync(geminiUrl, content);
                if (geminiResponse.IsSuccessStatusCode)
                {
                    var geminiJson = await geminiResponse.Content.ReadAsStringAsync();
                    var geminiResult = JsonSerializer.Deserialize<GeminiApiResponse>(geminiJson, options);
                    GeminiAdvice = geminiResult?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? "No advice available";
                }
                else
                {
                    GeminiAdvice = "Could not fetch AI advice at this time.";
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    ErrorMessage = $"An error occurred: {ex.Message}");
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    IsLoading = false);
            }
        }

        private DateTime UnixTimeToDateTime(long unixTime)
        {
            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(unixTime);
            return dateTimeOffset.DateTime;
        }
    }
}