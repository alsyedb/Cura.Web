namespace Cura.Web.Models
{
    public record Observation(string Code, string Value, DateTime Date, string RefId);
    public record Medication(string Name);
    public record Condition(string Name);

    public class Patient
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Gender { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public List<Condition> Conditions { get; set; } = new();
        public List<Medication> Medications { get; set; } = new();
        public List<Observation> Observations { get; set; } = new();
        public string LastNote { get; set; } = "";
    }
}
