namespace UFC.Events.Api.Models;

/// <summary>
/// Redis konfigürasyon modeli
/// </summary>
public class RedisConfiguration
{
    /// <summary>
    /// Redis bağlantı string'i
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Kullanılacak database numarası
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Redis instance adı (key prefix için kullanılır)
    /// </summary>
    public string InstanceName { get; set; } = "UFCEventsApi";
}
