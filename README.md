# ApiAggregator — API Aggregation Service

ApiAggregator is a scalable, resilient, and high-performance ASP.NET Core Web API that consolidates data from multiple external APIs (Open-Meteo, Hacker News, and GitHub) and delivers them through a unified endpoint. The service incorporates advanced features such as thread-safe request statistics, caching, Polly resilience, performance anomaly detection, and secure JWT-based authentication.

---

## 🏗️ Architecture & Features

The service is built on clean, modern design principles with C# and .NET 8. Below is a high-level overview of the system architecture:

```
                      +-------------------+
                      |   HTTP Client     |
                      +---------+---------+
                                |
                                | (JWT Bearer Token required)
                                v
                      +---------+---------+
                      |  API Controllers  |
                      +----+---------+----+
                           |         |
      (Query / api/statistics)       | (Query / api/aggregation)
                           v         v
             +-------------+---+ +---+-------------+
             | Statistics      | | Aggregation     |
             | Service         | | Controller      |
             +-------------+---+ +---+-------------+
                           |         |
                           |         | (Task.WhenAll parallel fetch)
                           |         v
                           |     +---+-------------+
                           |     | ICacheService   | <---+ Configurable TTLs
                           |     +---+-------------+     | (Weather: 60s, News: 120s,
                           |         |                   |  GitHub: 180s)
                           |         v
                           |     +---+-------------+
                           |     | Resilience      | <---+ Polly Pipelines
                           |     | Policies        |     | (Timeout, Retry,
                           |     +---+-------------+     |  Circuit Breaker)
                           |         |
                           v         v
                      +----+---------+----+
                      |   External APIs   |
                      |   (Weather, HN,   |
                      |    GitHub)        |
                      +-------------------+
```

### Key Technical Features:
* **Concurrency**: Concurrent data fetching using `Task.WhenAll` to minimize overall API latency.
* **Resilience Patterns (Polly)**:
  * **Timeout**: Short timeouts per service to avoid slow external servers blocking client threads.
  * **Exponential Backoff Retry**: Automatic retries for transient HTTP errors.
  * **Circuit Breaker**: Stops calling external APIs after a specific failure rate, shielding external systems and preventing socket starvation.
  * **Graceful Fallbacks**: Serves predefined fallback mock responses if the cache is empty and downstream requests fail.
* **Caching Strategy**: Custom `ICacheService` abstraction over `IMemoryCache` that caches per-service responses with individual, configurable Time-To-Live (TTL) durations.
* **Thread-Safe Statistics Tracking**: Maintains real-time statistics (total, successful, failed requests, average response times, and performance buckets) using `ConcurrentDictionary` and `Interlocked` atomic operations.
* **Background Anomaly Detection**: A hosted background service that polls metrics every 10 seconds, comparing recent 5-minute average response times with historical overall averages. It logs a warning if a service's latency spikes above 150% of its historic average.
* **JWT Authentication**: Secured endpoints requiring bearer token authentication. Configuration is dynamically loaded via strongly-typed settings.

---

## 🛠️ Tech Stack & Prerequisites

* **Framework**: .NET 8 SDK (ASP.NET Core Web API)
* **Libraries**:
  * [Polly](https://github.com/App-vNext/Polly) (v8.7.0) — Resilience policies
  * [Moq](https://github.com/moq/moq4) (v4.20.72) — Test mocking
  * [xUnit](https://github.com/xunit/xunit) — Test runner & framework
* **Prerequisites**: [install .NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

---

## 🚀 Getting Started

### 1. Build the Solution
Restore NuGet packages and compile the codebase:
```bash
dotnet restore
dotnet build
```

### 2. Run the Unit Tests
Execute the comprehensive suite of 43 unit tests across services and controllers:
```bash
dotnet test
```

### 3. Start the Web API
Run the project in development mode:
```bash
dotnet run --project ApiAggregator.Api
```
By default, the application will launch at:
* HTTP: `http://localhost:5246`
* Swagger UI: `http://localhost:5246/swagger`

### 4. Postman Collection Import
A pre-configured Postman Collection is included in the repository root as [ApiAggregator.postman_collection.json](file:///c:/Users/astat/ApiAggregator/ApiAggregator.postman_collection.json).
1. Open Postman and click **Import**.
2. Select [ApiAggregator.postman_collection.json](file:///c:/Users/astat/ApiAggregator/ApiAggregator.postman_collection.json).
3. Set the `baseUrl` collection variable if yours differs from `http://localhost:5246`.
4. Trigger the **Login** request. A post-response test script will automatically capture the returned token and store it in your collection variable, so you can execute the other requests (`GET Aggregation`, `GET Statistics`, etc.) immediately without manual copy-pasting.

---

## 🔑 Authentication Flow

All endpoints under `/api/aggregation` and `/api/statistics` require a valid JWT bearer token.

1. **Obtain Token**: Send a login request.
2. **Add Header**: Supply the returned token in the HTTP `Authorization` header as:
   ```http
   Authorization: Bearer <your-jwt-token>
   ```

*(In Swagger, click the **Authorize** button on the top right, enter `Bearer <your-token>`, and click Authorize).*

---

## 📡 API Reference & Payload Examples

### 1. Authenticate (Login)
* **URL**: `POST /api/auth/login`
* **Request Body**:
  ```json
  {
    "username": "admin",
    "password": "password123"
  }
  ```
* **Response (200 OK)**:
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJ1bmlxdWVfbmFtZSI6ImFkbWluIi..."
  }
  ```

---

### 2. Retrieve Aggregated Data
Fetch consolidated data from the weather, news, and GitHub APIs in parallel.

* **URL**: `GET /api/aggregation`
* **Query Parameters (Optional)**:
  * `services` (string, comma-separated): Limit retrieval to specific sources (e.g. `services=weather,github`).
  * `keyword` (string): Search and filter news titles/authors and GitHub profile bio/company/username.
  * `sortBy` (string): Field to sort service metadata. Options: `name` or `duration`.
  * `sortOrder` (string): Order of sort. Options: `asc` or `desc` (default is `asc`).
* **Example Query**:
  `GET /api/aggregation?services=weather,news&keyword=dotnet&sortBy=duration&sortOrder=desc`

* **Response (200 OK)**:
  ```json
  {
    "weather": {
      "latitude": 52.52,
      "longitude": 13.41,
      "temperature": 18.5,
      "temperatureUnit": "°C",
      "windSpeed": 14.2,
      "windSpeedUnit": "km/h",
      "time": "2026-06-16T12:00:00"
    },
    "news": [
      {
        "title": "Announcing .NET 8 Performance Improvements",
        "author": "john_doe",
        "url": "https://devblogs.microsoft.com/dotnet/performance-improvements/",
        "score": 380
      }
    ],
    "github": null,
    "metadata": {
      "News": {
        "isSuccess": true,
        "responseTimeMs": 145,
        "errorMessage": null,
        "isCached": false
      },
      "Weather": {
        "isSuccess": true,
        "responseTimeMs": 85,
        "errorMessage": null,
        "isCached": true
      }
    }
  }
  ```

---

### 3. View API Statistics
Retrieve response times and request volumes for all tracked services.

* **URL**: `GET /api/statistics`
* **Response (200 OK)**:
  ```json
  [
    {
      "serviceName": "Weather",
      "totalRequests": 12,
      "successfulRequests": 12,
      "failedRequests": 0,
      "averageResponseTimeMs": 92.4,
      "buckets": {
        "fastCount": 10,
        "averageCount": 2,
        "slowCount": 0
      }
    },
    {
      "serviceName": "News",
      "totalRequests": 5,
      "successfulRequests": 4,
      "failedRequests": 1,
      "averageResponseTimeMs": 310.2,
      "buckets": {
        "fastCount": 1,
        "averageCount": 2,
        "slowCount": 2
      }
    }
  ]
  ```

---

### 4. Reset Statistics
Clears all recorded metrics and resets performance counters.

* **URL**: `POST /api/statistics/reset`
* **Response (200 OK)**:
  ```json
  {
    "message": "Statistics have been reset successfully."
  }
  ```

---

## ⚙️ Caching & Resilience Strategy

### Caching Strategy
The service exposes a cache settings section in the `appsettings.json` file. Cache hits do not incur external HTTP traffic or Polly policy evaluations, yielding fast response times (< 5ms).

Configured TTL limits:
```json
"Apis": {
  "Weather": { "CacheDurationSeconds": 60 },
  "News": { "CacheDurationSeconds": 120 },
  "GitHub": { "CacheDurationSeconds": 180 }
}
```

### Resilience Strategy
Polly is configured with three distinct layers per external service:
1. **Timeout**: If the external endpoint does not respond within a threshold (e.g. 5 seconds), the execution is aborted.
2. **Retry (Exponential Backoff)**: If a request fails or times out, it is retried (up to 3 times by default) with exponentially longer pauses in between.
3. **Circuit Breaker**: If 50% of requests fail within a 30-second window (after a minimum of 5 requests), the circuit is opened for 30 seconds. All requests during this open period fail fast immediately, preventing API request overload.

---

## 📊 Performance Buckets
Endpoints are categorized based on their duration:
* **Fast**: $< 100\text{ ms}$
* **Average**: $100\text{ ms} - 300\text{ ms}$
* **Slow**: $> 300\text{ ms}$

These buckets can be monitored using `GET /api/statistics` to diagnose downstream latency spikes.

---

## 🔌 Extensibility Guide: Adding a New Service

Integrating a new API service into the aggregator is simple and does not require modifications to the core aggregation flow:

### 1. Register Configuration
Add the new service configuration in `appsettings.json` under `Apis`:
```json
"Apis": {
  "MyNewApi": {
    "BaseUrl": "https://api.example.com/",
    "Endpoint": "v1/data",
    "CacheDurationSeconds": 300,
    "TimeoutSeconds": 3
  }
}
```

### 2. Define Output Models
Create the payload representation in the `Models` folder:
```csharp
public class MyNewApiResult
{
    public string DataField { get; set; } = string.Empty;
}
```

### 3. Implement `IExternalApiService`
Implement the service in `Services/` wrapping HttpClient, caching, and Polly resilience:
```csharp
using ApiAggregator.Api.Services.Interfaces;

public class MyNewApiService : IExternalApiService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;
    private readonly IResiliencePolicies _resiliencePolicies;
    private readonly ApiSettings _settings;

    public string ServiceName => "MyNewApi";

    public MyNewApiService(
        HttpClient httpClient,
        ICacheService cacheService,
        IResiliencePolicies resiliencePolicies,
        IOptions<ApiSettings> settings)
    {
        _httpClient = httpClient;
        _cacheService = cacheService;
        _resiliencePolicies = resiliencePolicies;
        _settings = settings.Value;
    }

    public async Task<object> FetchDataAsync(CancellationToken cancellationToken = default)
    {
        var config = _settings.Apis[ServiceName];
        var cacheKey = $"cache_{ServiceName.ToLowerInvariant()}";

        return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
        {
            var pipeline = _resiliencePolicies.GetPipeline(ServiceName);
            return await pipeline.ExecuteAsync(async token =>
            {
                var response = await _httpClient.GetAsync(config.Endpoint, token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<MyNewApiResult>(cancellationToken: token);
            }, cancellationToken);
        }, TimeSpan.FromSeconds(config.CacheDurationSeconds)) ?? GetFallbackData();
    }

    private MyNewApiResult GetFallbackData() => new() { DataField = "Fallback Data" };
}
```

### 4. Register in Dependency Injection
Open `Program.cs` and add the typed HTTP client:
```csharp
builder.Services.AddHttpClient<IExternalApiService, MyNewApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:Apis:MyNewApi:BaseUrl"]!);
});
```

The `AggregationController` will automatically detect the new service, fetch its data concurrently, track statistics, and map it directly into the aggregated metadata response!