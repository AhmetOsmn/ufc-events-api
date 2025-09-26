using HtmlAgilityPack;
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
        _logger.LogInformation("UFC events scraping başlatılıyor...");

        var html = await _httpClient.GetStringAsync(UFC_EVENTS_URL);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var eventNodes = GetEventNodes(doc);
        var events = new List<Event>();

        foreach (var eventNode in eventNodes)
        {
            try
            {
                var eventObj = await ParseEventFromNodeAsync(eventNode);
                if (eventObj != null)
                {
                    events.Add(eventObj);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Event parsing hatası: {Error}", ex.Message);
            }
        }

        _logger.LogInformation("{Count} adet event başarıyla parse edildi", events.Count);
        return events;
    }

    private HtmlNodeCollection GetEventNodes(HtmlDocument document)
    {
        return document.DocumentNode.SelectNodes("//details[@id='events-list-upcoming']//div[@class='l-listing__item views-row']");
    }

    private async Task<Event?> ParseEventFromNodeAsync(HtmlNode eventNode)
    {
        try
        {
            var eventObj = new Event();

            var detailUrl = GetEventDetailUrl(eventNode);
            if (string.IsNullOrEmpty(detailUrl)) return null;

            var detailHtml = await _httpClient.GetStringAsync(detailUrl);
            var detailDoc = new HtmlDocument();
            detailDoc.LoadHtml(detailHtml);

            // Event Title'ı event node'undan al
            var titleNode = eventNode.SelectSingleNode(".//h3[@class='c-card-event--result__headline']/a");
            if (titleNode != null)
            {
                eventObj.EventTitle = titleNode.InnerText?.Trim() ?? string.Empty;
            }

            // Event Date'i event node'undan al
            var dateNode = eventNode.SelectSingleNode(".//div[@class='c-card-event--result__date tz-change-data']");
            if (dateNode != null)
            {
                var timestampAttr = dateNode.GetAttributeValue("data-main-card-timestamp", "");
                if (!string.IsNullOrEmpty(timestampAttr) && long.TryParse(timestampAttr, out var timestamp))
                {
                    eventObj.EventDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                }
            }

            // Event Location'ı event node'undan al (Venue + City + State + Country)
            var venueNode = eventNode.SelectSingleNode(".//div[@class='e-p--small c-card-event--result__location']//h5");
            var cityNode = eventNode.SelectSingleNode(".//div[@class='e-p--small c-card-event--result__location']//span[@class='locality']");
            var stateNode = eventNode.SelectSingleNode(".//div[@class='e-p--small c-card-event--result__location']//span[@class='administrative-area']");
            var countryNode = eventNode.SelectSingleNode(".//div[@class='e-p--small c-card-event--result__location']//span[@class='country']");

            var locationParts = new List<string>();

            // Venue bilgisi
            if (venueNode != null && !string.IsNullOrWhiteSpace(venueNode.InnerText))
            {
                locationParts.Add(venueNode.InnerText.Trim());
            }

            // City, State, Country bilgilerini birleştir
            var addressParts = new List<string>();
            if (cityNode != null && !string.IsNullOrWhiteSpace(cityNode.InnerText))
            {
                addressParts.Add(cityNode.InnerText.Trim());
            }
            if (stateNode != null && !string.IsNullOrWhiteSpace(stateNode.InnerText))
            {
                addressParts.Add(stateNode.InnerText.Trim());
            }
            if (countryNode != null && !string.IsNullOrWhiteSpace(countryNode.InnerText))
            {
                addressParts.Add(countryNode.InnerText.Trim());
            }

            if (addressParts.Count > 0)
            {
                locationParts.Add(string.Join(" ", addressParts));
            }

            eventObj.EventLocation = locationParts.Count > 0 ? string.Join(" - ", locationParts) : string.Empty;

            // Fight'ları detay sayfasından al
            var fightNodes = detailDoc.DocumentNode.SelectNodes("//li[@class='l-listing__item']");
            if (fightNodes != null)
            {
                var fights = new List<Fight>();
                for (int i = 0; i < fightNodes.Count; i++)
                {
                    // Order'ı tersine çevir: HTML'deki ilk fight en son oynanacak fight olduğu için en yüksek order'a sahip olmalı
                    var order = fightNodes.Count - i;
                    var fight = ParseFightFromDetailNode(fightNodes[i], order);
                    if (fight != null)
                    {
                        fights.Add(fight);
                    }
                }
                eventObj.Fights = fights;
            }

            return eventObj;
        }
        catch (Exception ex)
        {
            _logger.LogError("Event node parsing hatası: {Error}", ex.Message);
            return null;
        }
    }

    private Fight? ParseFightFromDetailNode(HtmlNode fightNode, int order)
    {
        var fight = new Fight { Order = order };

        // Weight class bilgisini al
        var weightClassNode = fightNode.SelectSingleNode(".//div[@class='c-listing-fight__class-text']");
        if (weightClassNode != null)
        {
            fight.WeightClass = weightClassNode.InnerText?.Trim() ?? string.Empty;
        }

        var fighters = new List<Fighter>();

        // Red corner fighter'ı parse et
        var redFighter = ParseFighterFromDetailNode(fightNode, "red");
        if (redFighter != null)
        {
            fighters.Add(redFighter);
        }

        var blueFighter = ParseFighterFromDetailNode(fightNode, "blue");
        if (blueFighter != null)
        {
            fighters.Add(blueFighter);
        }

        fight.Fighters = fighters;

        return fight.Fighters.Count >= 2 ? fight : null;
    }

    private static Fighter? ParseFighterFromDetailNode(HtmlNode fightNode, string corner)
    {
        var fighter = new Fighter();

        // Fighter name'ini al (given name + family name)
        var givenNameNode = fightNode.SelectSingleNode($".//div[@class='c-listing-fight__corner-name c-listing-fight__corner-name--{corner}']//span[@class='c-listing-fight__corner-given-name']");
        var familyNameNode = fightNode.SelectSingleNode($".//div[@class='c-listing-fight__corner-name c-listing-fight__corner-name--{corner}']//span[@class='c-listing-fight__corner-family-name']");

        if (givenNameNode != null && familyNameNode != null)
        {
            var givenName = givenNameNode.InnerText?.Trim() ?? "";
            var familyName = familyNameNode.InnerText?.Trim() ?? "";
            fighter.Name = $"{givenName} {familyName}";
        }

        // Fighter ranking'ini al
        var rankNodes = fightNode.SelectNodes(".//div[@class='js-listing-fight__corner-rank c-listing-fight__corner-rank']/span");
        if (rankNodes != null && rankNodes.Count >= 2)
        {
            var rankText = corner == "red" ? rankNodes[0].InnerText?.Trim() : rankNodes[1].InnerText?.Trim();
            if (!string.IsNullOrEmpty(rankText) && rankText.StartsWith("#"))
            {
                if (int.TryParse(rankText.Substring(1), out var rank))
                {
                    fighter.Ranking = rank;
                }
            }
        }

        // Fighter country'sini al
        var countryNode = fightNode.SelectSingleNode($".//div[@class='c-listing-fight__country c-listing-fight__country--{corner}']//div[@class='c-listing-fight__country-text']");
        if (countryNode != null)
        {
            fighter.Country = countryNode.InnerText?.Trim() ?? "";
        }

        return fighter;
    }

    private static string? GetEventDetailUrl(HtmlNode eventNode)
    {
        var detailLinkNode = eventNode.SelectSingleNode(".//div[@class='btn-container event-details-button']/a");
        if (detailLinkNode != null)
        {
            var href = detailLinkNode.GetAttributeValue("href", "");
            if (!string.IsNullOrEmpty(href))
            {
                if (href.StartsWith("/"))
                {
                    return $"https://www.ufc.com{href}";
                }
                return href;
            }
        }
        return null;
    }
}
