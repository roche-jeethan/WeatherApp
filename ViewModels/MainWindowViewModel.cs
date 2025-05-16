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

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand RefreshCommand { get; }

        public MainWindowViewModel()
        {
            _client = new HttpClient();
            Env.Load();

            // Initialize commands
            SearchCommand = ReactiveCommand.CreateFromTask(SearchWeatherAsync);
            RefreshCommand = ReactiveCommand.CreateFromTask(async () =>
                {
                    if (!string.IsNullOrEmpty(CityName))
                        await SearchWeatherAsync();
                });
        }

        private async Task SearchWeatherAsync()
        {
            if (string.IsNullOrWhiteSpace(CityName))
            {
                ErrorMessage = "Please enter a city name";
                return;
            }

            IsLoading = true;
            ErrorMessage = "";

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

                Temperature = $"{weather.Main.Temp:F1}°C";
                Humidity = $"{weather.Main.Humidity}%";

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
                ErrorMessage = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}