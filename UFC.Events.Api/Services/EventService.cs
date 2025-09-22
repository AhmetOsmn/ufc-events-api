using UFC.Events.Api.Models;

namespace UFC.Events.Api.Services;

public class EventService : IEventService
{
    private readonly IRedisCacheManager _cacheManager;
    private readonly ILogger<EventService> _logger;
    private const string EventsCacheKey = "ufc:events";

    public EventService(IRedisCacheManager cacheManager, ILogger<EventService> logger)
    {
        _cacheManager = cacheManager;
        _logger = logger;
    }

    public async Task<List<Event>> GetAllEventsAsync()
    {
        try
        {
            var events = await _cacheManager.GetAsync<List<Event>>(EventsCacheKey);
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

    public async Task SeedMockDataAsync()
    {
        try
        {
            // Cache'de zaten veri varsa tekrar ekleme
            var existingEvents = await _cacheManager.GetAsync<List<Event>>(EventsCacheKey);
            if (existingEvents != null && existingEvents.Count > 0)
            {
                _logger.LogInformation("Cache'de zaten {Count} adet event mevcut", existingEvents.Count);
                return;
            }

            var mockEvents = new List<Event>
            {
                new Event
                {
                    Id = "ufc-310",
                    EventDate = DateTime.Now.AddDays(30),
                    EventTitle = "UFC 310: Pantoja vs Asakura",
                    EventLocation = "Las Vegas, Nevada, USA",
                    Fights = new List<Fight>
                    {
                        new Fight
                        {
                            WeightClass = "Flyweight Championship",
                            Order = 1,
                            Fighters = new List<Fighter>
                            {
                                new Fighter
                                {
                                    Name = "Alexandre Pantoja",
                                    Country = "Brazil",
                                    Ranking = 1,
                                    Record = "28-5-0"
                                },
                                new Fighter
                                {
                                    Name = "Kai Asakura",
                                    Country = "Japan",
                                    Ranking = null,
                                    Record = "21-4-0"
                                }
                            }
                        },
                        new Fight
                        {
                            WeightClass = "Welterweight",
                            Order = 2,
                            Fighters = new List<Fighter>
                            {
                                new Fighter
                                {
                                    Name = "Shavkat Rakhmonov",
                                    Country = "Kazakhstan",
                                    Ranking = 3,
                                    Record = "18-0-0"
                                },
                                new Fighter
                                {
                                    Name = "Ian Machado Garry",
                                    Country = "Ireland",
                                    Ranking = 7,
                                    Record = "15-0-0"
                                }
                            }
                        },
                        new Fight
                        {
                            WeightClass = "Heavyweight",
                            Order = 3,
                            Fighters = new List<Fighter>
                            {
                                new Fighter
                                {
                                    Name = "Ciryl Gane",
                                    Country = "France",
                                    Ranking = 2,
                                    Record = "12-2-0"
                                },
                                new Fighter
                                {
                                    Name = "Alexander Volkov",
                                    Country = "Russia",
                                    Ranking = 4,
                                    Record = "38-10-0"
                                }
                            }
                        }
                    }
                },
                new Event
                {
                    Id = "ufc-311",
                    EventDate = DateTime.Now.AddDays(60),
                    EventTitle = "UFC 311: Islam vs Arman",
                    EventLocation = "Los Angeles, California, USA",
                    Fights = new List<Fight>
                    {
                        new Fight
                        {
                            WeightClass = "Lightweight Championship",
                            Order = 1,
                            Fighters = new List<Fighter>
                            {
                                new Fighter
                                {
                                    Name = "Islam Makhachev",
                                    Country = "Russia",
                                    Ranking = 1,
                                    Record = "26-1-0"
                                },
                                new Fighter
                                {
                                    Name = "Arman Tsarukyan",
                                    Country = "Armenia",
                                    Ranking = 2,
                                    Record = "22-3-0"
                                }
                            }
                        },
                        new Fight
                        {
                            WeightClass = "Bantamweight Championship",
                            Order = 2,
                            Fighters = new List<Fighter>
                            {
                                new Fighter
                                {
                                    Name = "Sean O'Malley",
                                    Country = "USA",
                                    Ranking = 1,
                                    Record = "18-1-0"
                                },
                                new Fighter
                                {
                                    Name = "Umar Nurmagomedov",
                                    Country = "Russia",
                                    Ranking = 2,
                                    Record = "18-0-0"
                                }
                            }
                        }
                    }
                }
            };

            await SetEventsAsync(mockEvents);
            _logger.LogInformation("Mock data başarıyla cache'e eklendi: {Count} event", mockEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock data eklenirken hata oluştu");
            throw;
        }
    }
}
