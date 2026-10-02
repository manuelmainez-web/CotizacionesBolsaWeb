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

        _localFolder = ResolveLocalFolder(environment);
        Directory.CreateDirectory(_localFolder);
        MigrateExistingLocalData(environment.ContentRootPath);
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

    public async Task<bool> SaveEntriesAsync<T>(string key, List<T> entries)
    {
        if (_httpClient != null)
        {
            var json = JsonSerializer.Serialize(entries);
            try
            {
                var url = $"{_restUrl!.TrimEnd('/')}/set/{Uri.EscapeDataString(key)}";
                using var content = new StringContent(json, Encoding.UTF8, "text/plain");
                using var response = await _httpClient.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                // Si falla la escritura remota, no se interrumpe la petición del usuario,
                // pero se informa al llamador para que pueda avisar si lo necesita.
                return false;
            }
        }

        var localJson = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(GetLocalPath(key), localJson);
        return true;
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

    private static string ResolveLocalFolder(IWebHostEnvironment environment)
    {
        var candidates = new List<string>
        {
            Path.Combine(environment.ContentRootPath, "App_Data"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data")
        };

        var current = new DirectoryInfo(environment.ContentRootPath);
        while (current != null)
        {
            candidates.Add(Path.Combine(current.FullName, "App_Data"));
            if (File.Exists(Path.Combine(current.FullName, "CotizacionesBolsaWeb.csproj")))
            {
                return Path.Combine(current.FullName, "App_Data");
            }

            current = current.Parent;
        }

        var fallback = candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(path => Directory.Exists(path) || path.Contains("CotizacionesBolsaWeb"));

        return fallback ?? Path.Combine(environment.ContentRootPath, "App_Data");
    }

    private void MigrateExistingLocalData(string contentRootPath)
    {
        var contentRootAppData = Path.Combine(contentRootPath, "App_Data");
        if (!string.Equals(Path.GetFullPath(contentRootAppData), Path.GetFullPath(_localFolder), StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(contentRootAppData))
            {
                foreach (var file in Directory.GetFiles(contentRootAppData))
                {
                    var targetPath = Path.Combine(_localFolder, Path.GetFileName(file));
                    if (!File.Exists(targetPath))
                    {
                        File.Copy(file, targetPath);
                    }
                }
            }
        }
    }

    private string GetLocalPath(string key) => Path.Combine(_localFolder, key + ".json");
}
