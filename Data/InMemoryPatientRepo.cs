using Cura.Web.Models;

namespace Cura.Web.Data
{
    public interface IPatientRepo
    {
        IEnumerable<Patient> GetAll();
        Patient? Get(string id);
    }

    public class InMemoryPatientRepo : IPatientRepo
    {
        private readonly List<Patient> _patients;

        public InMemoryPatientRepo()
        {
            _patients = new List<Patient>
        {
            new Patient {
                Id = "patient-001",
                FullName = "John Doe",
                Gender = "male",
                BirthDate = new DateTime(1980,5,14),
                Conditions = { new("Type 2 Diabetes"), new("Hypertension") },
                Medications = { new("Metformin 500mg daily"), new("Lisinopril 10mg daily") },
                Observations = {
                    new("HbA1c","8.2%", new DateTime(2025,8,1), "FHIR:Observation/123"),
                    new("BP","150/95 mmHg", new DateTime(2025,8,15), "FHIR:Observation/456")
                },
                LastNote = "Patient reports fatigue and poor diet control."
            },
            new Patient {
                Id = "patient-002",
                FullName = "Sara Ahmed",
                Gender = "female",
                BirthDate = new DateTime(1992,11,3),
                Conditions = { new("Hypothyroidism") },
                Medications = { new("Levothyroxine 50mcg daily") },
                Observations = {
                    new("TSH","6.1 mIU/L", new DateTime(2025,7,20), "FHIR:Observation/789"),
                    new("BP","118/72 mmHg", new DateTime(2025,8,12), "FHIR:Observation/790")
                },
                LastNote = "Follow-up for thyroid dose adjustment."
            }
        };
        }

        public IEnumerable<Patient> GetAll() => _patients;
        public Patient? Get(string id) => _patients.FirstOrDefault(p => p.Id == id);
    }
}
