namespace UFC.Events.Api.Services;

/// <summary>
/// Redis cache işlemleri için interface
/// </summary>
public interface IRedisCacheManager
{
    /// <summary>
    /// Belirtilen key ile string değer set eder
    /// </summary>
    Task SetStringAsync(string key, string value, TimeSpan? expiry = null);

    /// <summary>
    /// Belirtilen key ile object değer set eder (JSON olarak serialize edilir)
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);

    /// <summary>
    /// Belirtilen key'e karşılık gelen string değeri getirir
    /// </summary>
    Task<string?> GetStringAsync(string key);

    /// <summary>
    /// Belirtilen key'e karşılık gelen object değeri getirir (JSON'dan deserialize edilir)
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Belirtilen key'in cache'de var olup olmadığını kontrol eder
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// Belirtilen key'i cache'den siler
    /// </summary>
    Task<bool> RemoveAsync(string key);

    /// <summary>
    /// Belirtilen pattern'e uyan tüm key'leri siler
    /// </summary>
    Task<long> RemoveByPatternAsync(string pattern);

    /// <summary>
    /// Belirtilen key'in TTL (Time To Live) değerini getirir
    /// </summary>
    Task<TimeSpan?> GetTtlAsync(string key);

    /// <summary>
    /// Belirtilen key'in expiry süresini günceller
    /// </summary>
    Task<bool> ExpireAsync(string key, TimeSpan expiry);

    /// <summary>
    /// Hash field set eder
    /// </summary>
    Task SetHashAsync(string key, string field, string value);

    /// <summary>
    /// Hash field getirir
    /// </summary>
    Task<string?> GetHashAsync(string key, string field);

    /// <summary>
    /// Hash'in tüm field'larını getirir
    /// </summary>
    Task<Dictionary<string, string>> GetHashAllAsync(string key);

    /// <summary>
    /// List'e element ekler
    /// </summary>
    Task<long> ListPushAsync(string key, string value);

    /// <summary>
    /// List'ten element çeker
    /// </summary>
    Task<string?> ListPopAsync(string key);

    /// <summary>
    /// List'in uzunluğunu getirir
    /// </summary>
    Task<long> ListLengthAsync(string key);
}
