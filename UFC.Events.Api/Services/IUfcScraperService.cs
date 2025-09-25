using UFC.Events.Api.Models;

namespace UFC.Events.Api.Services;

public interface IUfcScraperService
{
    Task<List<Event>> ScrapeUpcomingEventsAsync();
}
