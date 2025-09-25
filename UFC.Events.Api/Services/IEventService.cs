using UFC.Events.Api.Models;

namespace UFC.Events.Api.Services;

public interface IEventService
{
    Task<List<Event>> GetAllEventsAsync();
    Task<Event?> GetEventByIdAsync(string id);
    Task SetEventsAsync(List<Event> events);
    Task LoadLatestEventsAsync();
}
