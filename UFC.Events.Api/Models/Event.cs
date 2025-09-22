namespace UFC.Events.Api.Models;

public class Event
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime EventDate { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;
    public List<Fight> Fights { get; set; } = new List<Fight>();
}
