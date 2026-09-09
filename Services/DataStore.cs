using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CotizacionesBolsaWeb.Services;

/// <summary>
/// Almacén de datos con persistencia real: usa Upstash Redis (REST API) si hay credenciales
/// configuradas (Upstash:RestUrl / Upstash:RestToken); si no, cae a ficheros JSON locales en App_Data
/// (comportamiento original, útil para desarrollo local sin depender de servicios externos).
/// </summary>
public sealed class DataStore
{
    private readonly HttpClient? _httpClient;
    private readonly string? _restUrl;
    private readonly string _localFolder;

    public DataStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _restUrl = configuration["Upstash:RestUrl"];
        var restToken = configuration["Upstash:RestToken"];

        if (!string.IsNullOrWhiteSpace(_restUrl) && !string.IsNullOrWhiteSpace(restToken))
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", restToken);
        }

        _localFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(_localFolder);
    }

    public bool UsesRemoteStorage => _httpClient != null;

    public async Task<bool> ExistsAsync(string key)
    {
        if (_httpClient != null)
        {
            var raw = await GetRawAsync(key);
            return raw != null;
        }

        return File.Exists(GetLocalPath(key));
    }

    public async Task<List<T>> LoadEntriesAsync<T>(string key)
    {
        string? json;
        if (_httpClient != null)
        {
            json = await GetRawAsync(key);
        }
        else
        {
            var path = GetLocalPath(key);
            json = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<T>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
    }

    public async Task SaveEntriesAsync<T>(string key, List<T> entries)
    {
        if (_httpClient != null)
        {
            var json = JsonSerializer.Serialize(entries);
            try
            {
                var url = $"{_restUrl!.TrimEnd('/')}/set/{Uri.EscapeDataString(key)}";
                using var content = new StringContent(json, Encoding.UTF8, "text/plain");
                await _httpClient.PostAsync(url, content);
            }
            catch
            {
                // Si falla la escritura remota, no se interrumpe la petición del usuario.
            }

            return;
        }

        var localJson = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(GetLocalPath(key), localJson);
    }

    private async Task<string?> GetRawAsync(string key)
    {
        try
        {
            var url = $"{_restUrl!.TrimEnd('/')}/get/{Uri.EscapeDataString(key)}";
            using var response = await _httpClient!.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("result", out var resultElement) || resultElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return resultElement.GetString();
        }
        catch
        {
            return null;
        }
    }

    private string GetLocalPath(string key) => Path.Combine(_localFolder, key + ".json");
}
