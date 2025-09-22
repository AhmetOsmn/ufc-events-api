using StackExchange.Redis;
using System.Text.Json;

namespace UFC.Events.Api.Services;

/// <summary>
/// Redis cache işlemleri için concrete implementation
/// </summary>
public class RedisCacheManager : IRedisCacheManager
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisCacheManager> _logger;

    public RedisCacheManager(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCacheManager> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = _connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            await _database.StringSetAsync(key, value, expiry);
            _logger.LogDebug("Redis key set: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis SetStringAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, json, expiry);
            _logger.LogDebug("Redis object set: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis SetAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<string?> GetStringAsync(string key)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            _logger.LogDebug("Redis key retrieved: {Key}, HasValue: {HasValue}", key, value.HasValue);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GetStringAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            if (!value.HasValue)
            {
                _logger.LogDebug("Redis key not found: {Key}", key);
                return null;
            }

            var result = JsonSerializer.Deserialize<T>(value!);
            _logger.LogDebug("Redis object retrieved: {Key}", key);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GetAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var exists = await _database.KeyExistsAsync(key);
            _logger.LogDebug("Redis key exists check: {Key}, Exists: {Exists}", key, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ExistsAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        try
        {
            var result = await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Redis key removed: {Key}, Success: {Success}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis RemoveAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<long> RemoveByPatternAsync(string pattern)
    {
        try
        {
            var endpoints = _connectionMultiplexer.GetEndPoints();
            var server = _connectionMultiplexer.GetServer(endpoints.First());
            
            var keys = server.Keys(pattern: pattern).ToArray();
            if (keys.Length == 0)
            {
                _logger.LogDebug("No keys found for pattern: {Pattern}", pattern);
                return 0;
            }

            var result = await _database.KeyDeleteAsync(keys);
            _logger.LogDebug("Redis keys removed by pattern: {Pattern}, Count: {Count}", pattern, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis RemoveByPatternAsync operation failed for pattern: {Pattern}", pattern);
            throw;
        }
    }

    public async Task<TimeSpan?> GetTtlAsync(string key)
    {
        try
        {
            var ttl = await _database.KeyTimeToLiveAsync(key);
            _logger.LogDebug("Redis TTL retrieved: {Key}, TTL: {TTL}", key, ttl);
            return ttl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GetTtlAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<bool> ExpireAsync(string key, TimeSpan expiry)
    {
        try
        {
            var result = await _database.KeyExpireAsync(key, expiry);
            _logger.LogDebug("Redis expiry set: {Key}, Expiry: {Expiry}, Success: {Success}", key, expiry, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ExpireAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task SetHashAsync(string key, string field, string value)
    {
        try
        {
            await _database.HashSetAsync(key, field, value);
            _logger.LogDebug("Redis hash field set: {Key}, Field: {Field}", key, field);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis SetHashAsync operation failed for key: {Key}, field: {Field}", key, field);
            throw;
        }
    }

    public async Task<string?> GetHashAsync(string key, string field)
    {
        try
        {
            var value = await _database.HashGetAsync(key, field);
            _logger.LogDebug("Redis hash field retrieved: {Key}, Field: {Field}, HasValue: {HasValue}", key, field, value.HasValue);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GetHashAsync operation failed for key: {Key}, field: {Field}", key, field);
            throw;
        }
    }

    public async Task<Dictionary<string, string>> GetHashAllAsync(string key)
    {
        try
        {
            var hash = await _database.HashGetAllAsync(key);
            var result = hash.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
            _logger.LogDebug("Redis hash retrieved: {Key}, FieldCount: {Count}", key, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis GetHashAllAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<long> ListPushAsync(string key, string value)
    {
        try
        {
            var result = await _database.ListLeftPushAsync(key, value);
            _logger.LogDebug("Redis list push: {Key}, NewLength: {Length}", key, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ListPushAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<string?> ListPopAsync(string key)
    {
        try
        {
            var value = await _database.ListLeftPopAsync(key);
            _logger.LogDebug("Redis list pop: {Key}, HasValue: {HasValue}", key, value.HasValue);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ListPopAsync operation failed for key: {Key}", key);
            throw;
        }
    }

    public async Task<long> ListLengthAsync(string key)
    {
        try
        {
            var length = await _database.ListLengthAsync(key);
            _logger.LogDebug("Redis list length: {Key}, Length: {Length}", key, length);
            return length;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis ListLengthAsync operation failed for key: {Key}", key);
            throw;
        }
    }
}
