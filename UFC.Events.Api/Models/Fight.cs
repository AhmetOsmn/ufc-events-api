namespace UFC.Events.Api.Models;

public class Fight
{
    public string WeightClass { get; set; } = string.Empty; // e.g: "Lightweight", "Heavyweight"
    public int Order { get; set; } // fight order
    public List<Fighter> Fighters { get; set; } = new List<Fighter>();
}
