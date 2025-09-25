using HtmlAgilityPack;
using System.Globalization;
using System.Text.RegularExpressions;
using UFC.Events.Api.Models;

namespace UFC.Events.Api.Services;

public class UfcScraperService : IUfcScraperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UfcScraperService> _logger;
    private const string UFC_EVENTS_URL = "https://www.ufc.com/events";

    public UfcScraperService(HttpClient httpClient, ILogger<UfcScraperService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<List<Event>> ScrapeUpcomingEventsAsync()
    {
        try
        {
            _logger.LogInformation("UFC events scraping başlatılıyor...");
            
            var html = await _httpClient.GetStringAsync(UFC_EVENTS_URL);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var events = new List<Event>();
            
            // UFC events page'deki event card'larını bul
            var eventCards = doc.DocumentNode
                .SelectNodes("//div[contains(@class, 'c-card-event')]") ?? 
                doc.DocumentNode.SelectNodes("//div[contains(@class, 'event-card')]") ??
                doc.DocumentNode.SelectNodes("//div[contains(@class, 'view-upcoming-events')]//div[contains(@class, 'views-row')]");

            if (eventCards == null || !eventCards.Any())
            {
                _logger.LogWarning("Event card'lar bulunamadı, alternatif selector deneniyor...");
                
                // Alternatif selector'lar
                eventCards = doc.DocumentNode
                    .SelectNodes("//article[contains(@class, 'event')]") ??
                    doc.DocumentNode.SelectNodes("//div[contains(@class, 'upcoming')]//div[contains(@class, 'event')]");
            }

            if (eventCards != null)
            {
                foreach (var card in eventCards.Take(10)) // İlk 10 eventi al
                {
                    try
                    {
                        var eventData = ExtractEventData(card);
                        if (eventData != null)
                        {
                            events.Add(eventData);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Event card parse edilirken hata: {Html}", card.OuterHtml.Substring(0, Math.Min(200, card.OuterHtml.Length)));
                    }
                }
            }

            // Eğer hiç event bulunamadıysa fallback data oluştur
            if (!events.Any())
            {
                _logger.LogWarning("Hiç event scrape edilemedi, fallback data oluşturuluyor...");
                events = CreateFallbackEvents();
            }

            _logger.LogInformation("{Count} adet event başarıyla scrape edildi", events.Count);
            return events;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UFC events scraping sırasında hata oluştu");
            
            // Hata durumunda fallback data döndür
            return CreateFallbackEvents();
        }
    }

    private Event? ExtractEventData(HtmlNode cardNode)
    {
        try
        {
            // Event title
            var titleNode = cardNode.SelectSingleNode(".//h3") ?? 
                           cardNode.SelectSingleNode(".//h2") ?? 
                           cardNode.SelectSingleNode(".//*[contains(@class, 'title')]//text()") ?? 
                           cardNode.SelectSingleNode(".//*[contains(@class, 'event-title')]");
            
            var title = titleNode?.InnerText?.Trim() ?? "UFC Event";

            // Event date
            var dateNode = cardNode.SelectSingleNode(".//*[contains(@class, 'date')]") ?? 
                          cardNode.SelectSingleNode(".//*[@datetime]") ?? 
                          cardNode.SelectSingleNode(".//*[contains(@class, 'time')]");
            
            var dateText = dateNode?.InnerText?.Trim() ?? dateNode?.GetAttributeValue("datetime", "") ?? "";
            var eventDate = ParseEventDate(dateText);

            // Event location
            var locationNode = cardNode.SelectSingleNode(".//*[contains(@class, 'location')]") ?? 
                              cardNode.SelectSingleNode(".//*[contains(@class, 'venue')]") ?? 
                              cardNode.SelectSingleNode(".//*[contains(@class, 'city')]");
            
            var location = locationNode?.InnerText?.Trim() ?? "TBD";

            // Fight card bilgileri (varsa)
            var fights = ExtractFights(cardNode);

            return new Event
            {
                Id = GenerateEventId(title),
                EventTitle = CleanText(title),
                EventDate = eventDate,
                EventLocation = CleanText(location),
                Fights = fights
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Event data extraction sırasında hata");
            return null;
        }
    }

    private List<Fight> ExtractFights(HtmlNode cardNode)
    {
        var fights = new List<Fight>();
        
        try
        {
            // Main event ve featured fights
            var fightNodes = cardNode.SelectNodes(".//*[contains(@class, 'fight')]") ?? 
                           cardNode.SelectNodes(".//*[contains(@class, 'bout')]") ?? 
                           cardNode.SelectNodes(".//*[contains(@class, 'matchup')]");

            if (fightNodes != null)
            {
                int order = 1;
                foreach (var fightNode in fightNodes.Take(5)) // Maksimum 5 fight
                {
                    var fight = ExtractSingleFight(fightNode, order);
                    if (fight != null)
                    {
                        fights.Add(fight);
                        order++;
                    }
                }
            }

            // Eğer fight bulunamadıysa, fighter isimlerini bul
            if (!fights.Any())
            {
                var fighterNames = cardNode.SelectNodes(".//*[contains(@class, 'fighter')]") ?? 
                                 cardNode.SelectNodes(".//*[contains(@class, 'athlete')]");

                if (fighterNames != null && fighterNames.Count >= 2)
                {
                    var mainEvent = new Fight
                    {
                        WeightClass = "Main Event",
                        Order = 1,
                        Fighters = fighterNames.Take(2).Select(fn => new Fighter 
                        { 
                            Name = CleanText(fn.InnerText),
                            Country = "TBD",
                            Record = "TBD"
                        }).ToList()
                    };
                    fights.Add(mainEvent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fight extraction sırasında hata");
        }

        return fights;
    }

    private Fight? ExtractSingleFight(HtmlNode fightNode, int order)
    {
        try
        {
            var fighters = new List<Fighter>();
            
            // Fighter isimlerini bul
            var fighterNodes = fightNode.SelectNodes(".//*[contains(@class, 'fighter-name')]") ?? 
                             fightNode.SelectNodes(".//span[contains(@class, 'name')]") ?? 
                             fightNode.SelectNodes(".//div[contains(@class, 'athlete')]");

            if (fighterNodes != null)
            {
                foreach (var fighterNode in fighterNodes.Take(2))
                {
                    var name = CleanText(fighterNode.InnerText);
                    if (!string.IsNullOrEmpty(name))
                    {
                        fighters.Add(new Fighter
                        {
                            Name = name,
                            Country = "TBD",
                            Record = "TBD"
                        });
                    }
                }
            }

            // Weight class
            var weightClassNode = fightNode.SelectSingleNode(".//*[contains(@class, 'weight')]") ?? 
                                fightNode.SelectSingleNode(".//*[contains(@class, 'division')]");
            
            var weightClass = weightClassNode?.InnerText?.Trim() ?? (order == 1 ? "Main Event" : "Fight");

            if (fighters.Count >= 2)
            {
                return new Fight
                {
                    WeightClass = CleanText(weightClass),
                    Order = order,
                    Fighters = fighters
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Single fight extraction sırasında hata");
        }

        return null;
    }

    private DateTime ParseEventDate(string dateText)
    {
        if (string.IsNullOrEmpty(dateText))
            return DateTime.Now.AddDays(30);

        try
        {
            // Çeşitli tarih formatlarını dene
            var formats = new[]
            {
                "yyyy-MM-dd",
                "MM/dd/yyyy",
                "dd/MM/yyyy",
                "MMM dd, yyyy",
                "MMMM dd, yyyy",
                "dd MMM yyyy",
                "dd MMMM yyyy"
            };

            // HTML datetime attribute
            if (DateTime.TryParse(dateText, out DateTime result))
                return result;

            // Regex ile tarih çıkar
            var dateRegex = new Regex(@"(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{4})|(\w+)\s+(\d{1,2}),?\s+(\d{4})");
            var match = dateRegex.Match(dateText);
            
            if (match.Success)
            {
                if (DateTime.TryParseExact(match.Value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                    return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tarih parse edilemedi: {DateText}", dateText);
        }

        // Default olarak 30 gün sonrası
        return DateTime.Now.AddDays(30);
    }

    private string GenerateEventId(string title)
    {
        if (string.IsNullOrEmpty(title))
            return Guid.NewGuid().ToString();

        // UFC XXX formatını bul
        var ufcMatch = Regex.Match(title, @"UFC\s+(\d+)", RegexOptions.IgnoreCase);
        if (ufcMatch.Success)
            return $"ufc-{ufcMatch.Groups[1].Value}";

        // Başlığı temizle ve ID yap
        var cleanTitle = Regex.Replace(title.ToLower(), @"[^\w\s-]", "")
                              .Replace(" ", "-")
                              .Replace("--", "-")
                              .Trim('-');

        return cleanTitle.Substring(0, Math.Min(50, cleanTitle.Length));
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return Regex.Replace(text.Trim(), @"\s+", " ");
    }

    private List<Event> CreateFallbackEvents()
    {
        _logger.LogInformation("Fallback events oluşturuluyor...");
        
        return new List<Event>
        {
            new Event
            {
                Id = "ufc-upcoming-1",
                EventDate = DateTime.Now.AddDays(30),
                EventTitle = "Upcoming UFC Event",
                EventLocation = "Las Vegas, Nevada, USA",
                Fights = new List<Fight>
                {
                    new Fight
                    {
                        WeightClass = "Main Event",
                        Order = 1,
                        Fighters = new List<Fighter>
                        {
                            new Fighter { Name = "Fighter A", Country = "USA", Record = "TBD" },
                            new Fighter { Name = "Fighter B", Country = "Brazil", Record = "TBD" }
                        }
                    }
                }
            }
        };
    }
}
