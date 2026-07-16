namespace HspCopier.Services.Ball;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using HspCopier.Core.Ball;
using Microsoft.Extensions.Logging;

/// <summary>
/// 悬浮球天气服务实现:
/// 每 30 秒串行执行:粗筛网络 → IP 反查坐标(6h 缓存) → Open-Meteo 拉取天气 → 映射 WeatherCondition → 通知 UI。
/// 失败时设置 NoNetwork / NoLocation / Unknown,连续 3 次失败切到 5 分钟节流。
/// </summary>
public sealed class BallWeatherService : IBallWeatherService, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ThrottledInterval = TimeSpan.FromMinutes(5);
    private const int ThrottleThreshold = 3;
    private static readonly TimeSpan GeoCacheTtl = TimeSpan.FromHours(6);
    private const int HttpTimeoutMs = 4000;

    private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(HttpTimeoutMs) };
    private readonly ILogger<BallWeatherService> _logger;
    private readonly DispatcherTimer _timer;
    private readonly object _sync = new();

    private WeatherCondition _current = WeatherCondition.Unknown;
    private (double Lat, double Lon, DateTime UpdatedAt) _geoCache;
    private int _failCount;
    private bool _throttled;
    private bool _running;
    private int _refreshGate; // 0 = idle, 1 = in progress

    public BallWeatherService(ILogger<BallWeatherService> logger)
    {
        _logger = logger;
        // 显式绑定到主 UI 线程 Dispatcher,即使本服务在非 UI 线程被构造也能正确触发 Tick
        var dispatcher = System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = Interval,
        };
        _timer.Tick += (_, _) => { _ = RefreshAsync(); };
    }

    public WeatherCondition Current
    {
        get { lock (_sync) return _current; }
    }

    public event EventHandler? Changed;

    public Task StartAsync()
    {
        if (_running) return Task.CompletedTask;
        _running = true;
        _logger.LogInformation("BallWeatherService started");
        _timer.Interval = Interval;
        _timer.Start();
        // 立即触发一次 Refresh(异步,不阻塞 Start)
        _ = Task.Run(RefreshAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!_running) return Task.CompletedTask;
        _running = false;
        _timer.Stop();
        _logger.LogInformation("BallWeatherService stopped");
        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        // 串行化,防止 30s 周期与手动刷新重叠
        if (Interlocked.CompareExchange(ref _refreshGate, 1, 0) != 0) return;
        try
        {
            await RefreshCoreAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _refreshGate, 0);
        }
    }

    private async Task RefreshCoreAsync()
    {
        var next = await DetectAsync();
        ApplyAndRaise(next);
    }

    private async Task<WeatherCondition> DetectAsync()
    {
        // 1. 粗筛网卡
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            _logger.LogDebug("Network unavailable (GetIsNetworkAvailable=false)");
            return WeatherCondition.NoNetwork;
        }

        // 2. IP 反查坐标(6h 缓存)
        var (lat, lon, ok) = await GetCoordinatesAsync();
        if (!ok)
        {
            _logger.LogWarning("Location detection failed");
            return WeatherCondition.NoLocation;
        }

        // 3. Open-Meteo 天气
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,weather_code,is_day&timezone=auto";
            using var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out var cur))
            {
                _logger.LogWarning("Open-Meteo response missing 'current'");
                return WeatherCondition.Unknown;
            }
            var code = cur.TryGetProperty("weather_code", out var wc) ? wc.GetInt32() : -1;
            var isDay = cur.TryGetProperty("is_day", out var d) ? d.GetInt32() : 1;
            var cond = MapWeatherCode(code, isDay);
            _logger.LogInformation("Weather detected: code={Code} is_day={IsDay} → {Cond}", code, isDay, cond);
            return cond;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open-Meteo request failed");
            // 网络/天气 API 失败视为无网
            return WeatherCondition.NoNetwork;
        }
    }

    private async Task<(double lat, double lon, bool ok)> GetCoordinatesAsync()
    {
        var cache = _geoCache;
        if (cache.UpdatedAt != default && DateTime.UtcNow - cache.UpdatedAt < GeoCacheTtl)
        {
            return (cache.Lat, cache.Lon, true);
        }

        try
        {
            // ipinfo.io 国内可达、匿名可用;loc 字段格式 "lat,lon",需 split
            using var resp = await _http.GetAsync("https://ipinfo.io/json");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // ipinfo.io 失败时返回 {"error":"..."},无 loc 字段
            if (root.TryGetProperty("error", out _)) return (0, 0, false);
            if (!root.TryGetProperty("loc", out var locEl) || locEl.ValueKind != JsonValueKind.String)
                return (0, 0, false);
            var parts = locEl.GetString()!.Split(',');
            if (parts.Length != 2) return (0, 0, false);
            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat))
                return (0, 0, false);
            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon))
                return (0, 0, false);
            _geoCache = (lat, lon, DateTime.UtcNow);
            _logger.LogInformation("Geo resolved via ipinfo.io: lat={Lat} lon={Lon}", lat, lon);
            return (lat, lon, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ipinfo.io request failed");
            return (0, 0, false);
        }
    }

    private static WeatherCondition MapWeatherCode(int code, int isDay)
    {
        return code switch
        {
            0 or 1 => isDay == 1 ? WeatherCondition.Sunny : WeatherCondition.NightClear,
            2 or 3 => WeatherCondition.Cloudy,
            45 or 48 => WeatherCondition.Foggy,
            >= 51 and <= 67 => WeatherCondition.Rainy,
            >= 80 and <= 82 => WeatherCondition.Rainy,
            >= 71 and <= 77 => WeatherCondition.Snowy,
            >= 85 and <= 86 => WeatherCondition.Snowy,
            >= 95 and <= 99 => WeatherCondition.Thunder,
            _ => WeatherCondition.Unknown,
        };
    }

    private void ApplyAndRaise(WeatherCondition next)
    {
        bool changed;
        lock (_sync)
        {
            changed = _current != next;
            _current = next;
            if (next == WeatherCondition.NoNetwork || next == WeatherCondition.NoLocation || next == WeatherCondition.Unknown)
            {
                _failCount++;
                if (!_throttled && _failCount >= ThrottleThreshold)
                {
                    _throttled = true;
                    _timer.Interval = ThrottledInterval;
                    _logger.LogWarning("Too many failures, throttling to {Min} min", ThrottledInterval.TotalMinutes);
                }
            }
            else
            {
                if (_throttled || _failCount > 0)
                {
                    _throttled = false;
                    _failCount = 0;
                    _timer.Interval = Interval;
                }
            }
        }
        if (changed) RaiseChanged();
    }

    private void RaiseChanged()
    {
        // 直接在 UI 线程触发(DispatcherTimer 本身是 UI 线程,Task.Run 中的 Refresh 需要切回)
        if (_timer.Dispatcher.CheckAccess())
            Changed?.Invoke(this, EventArgs.Empty);
        else
            _timer.Dispatcher.BeginInvoke(new Action(() => Changed?.Invoke(this, EventArgs.Empty)));
    }

    public void Dispose()
    {
        _timer.Stop();
        _http.Dispose();
    }
}
