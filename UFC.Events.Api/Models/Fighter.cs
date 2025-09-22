namespace UFC.Events.Api.Models;

public class Fighter
{
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int? Ranking { get; set; }
    public string Record { get; set; } = string.Empty; // e.g: "22-3-0"
}
