using UFC.Events.Api.Models;

namespace UFC.Events.Api.Services;

public class EventService : IEventService
{
    private readonly IRedisCacheManager _cacheManager;
    private readonly IUfcScraperService _ufcScraperService;
    private readonly ILogger<EventService> _logger;
    private const string EventsCacheKey = "ufc:events";

    public EventService(IRedisCacheManager cacheManager, IUfcScraperService ufcScraperService, ILogger<EventService> logger)
    {
        _cacheManager = cacheManager;
        _ufcScraperService = ufcScraperService;
        _logger = logger;
    }

    public async Task<List<Event>> GetAllEventsAsync()
    {
        try
        {
            var events = await _cacheManager.GetAsync<List<Event>>(EventsCacheKey);
            
            // Cache'de veri yoksa veya cache süresi dolmuşsa, fresh data çek
            if (events == null || !events.Any())
            {
                _logger.LogInformation("Cache'de event bulunamadı, fresh data çekiliyor...");
                await LoadLatestEventsAsync();
                events = await _cacheManager.GetAsync<List<Event>>(EventsCacheKey);
            }
            
            return events ?? new List<Event>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event'ler cache'den alınırken hata oluştu");
            return new List<Event>();
        }
    }

    public async Task<Event?> GetEventByIdAsync(string id)
    {
        try
        {
            var events = await GetAllEventsAsync();
            return events.FirstOrDefault(e => e.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event ID {Id} ile alınırken hata oluştu", id);
            return null;
        }
    }

    public async Task SetEventsAsync(List<Event> events)
    {
        try
        {
            await _cacheManager.SetAsync(EventsCacheKey, events, TimeSpan.FromHours(24));
            _logger.LogInformation("{Count} adet event cache'e eklendi", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event'ler cache'e eklenirken hata oluştu");
            throw;
        }
    }

    public async Task LoadLatestEventsAsync()
    {
        try
        {
            _logger.LogInformation("UFC web sitesinden fresh event data çekiliyor...");
            
            var events = await _ufcScraperService.ScrapeUpcomingEventsAsync();
            
            if (events != null && events.Any())
            {
                await SetEventsAsync(events);
                _logger.LogInformation("UFC web sitesinden {Count} adet event başarıyla cache'e eklendi", events.Count);
            }
            else
            {
                _logger.LogWarning("UFC web sitesinden event çekilemedi");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UFC event data yüklenirken hata oluştu");
            throw;
        }
    }
}
