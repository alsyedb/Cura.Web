namespace Cura.Web.Models
{
    public class PatientDetailsVM
    {
        public IEnumerable<Patient> Patients { get; set; } = Enumerable.Empty<Patient>();
        public Patient? Selected { get; set; }
    }
}
