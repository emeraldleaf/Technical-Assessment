using Bogus;
using SignalBooster.Models;

namespace SignalBooster.Tests.TestHelpers;

public static class TestDataFactory
{
    private static readonly Faker _faker = new();

    public static class PhysicianNotes
    {
        public static readonly (string Text, DeviceOrder Expected) OxygenTank = 
            PhysicianNoteBuilder.Create()
                .WithPatient("Harold Finch", "04/12/1952")
                .WithDiagnosis("COPD")
                .WithProvider("Dr. Cuddy")
                .WithDevice("Oxygen Tank")
                .WithOxygenSpecs("2 L", "sleep and exertion")
                .BuildTextAndExpected();

        public static readonly (string Text, DeviceOrder Expected) CpapWithAccessories =
            PhysicianNoteBuilder.Create()
                .WithPatient("Lisa Turner", "09/23/1984")
                .WithDiagnosis("Severe sleep apnea")
                .WithProvider("Dr. Foreman")
                .WithDevice("CPAP")
                .WithCpapSpecs("full face", "heated humidifier")
                .WithQualifier("AHI > 20")
                .BuildTextAndExpected();

        public static readonly (string Text, DeviceOrder Expected) SimpleCpap =
            PhysicianNoteBuilder.Create()
                .WithPatient("Unknown Patient", "Unknown DOB")
                .WithDiagnosis("sleep apnea")
                .WithProvider("Dr. Smith")
                .WithDevice("CPAP")
                .BuildTextAndExpected();

        public static readonly (string Text, DeviceOrder Expected) HospitalBed =
            PhysicianNoteBuilder.Create()
                .WithPatient("Maria Gonzalez", "03/15/1965")
                .WithDiagnosis("Post-surgical recovery, mobility limitations")
                .WithProvider("Dr. Stevens")
                .WithDevice("Hospital Bed")
                .WithCpapSpecs("", "side rails", "pressure relief")
                .WithQualifier("pressure sore risk")
                .BuildTextAndExpected();

        public static readonly (string Text, DeviceOrder Expected) TensUnit =
            PhysicianNoteBuilder.Create()
                .WithPatient("James Cooper", "09/07/1964")
                .WithDiagnosis("Chronic lower back pain, muscle spasms")
                .WithProvider("Dr. Garcia")
                .WithDevice("TENS Unit")
                .BuildTextAndExpected();
    }

    public static DeviceOrder CreateRandomDeviceOrder()
    {
        var devices = new[] { "CPAP", "Oxygen Tank", "Hospital Bed", "TENS Unit", "Nebulizer" };
        
        return new DeviceOrder
        {
            PatientName = _faker.Name.FullName(),
            Dob = _faker.Date.Past(80, DateTime.Now.AddYears(-18)).ToString("MM/dd/yyyy"),
            Diagnosis = _faker.Lorem.Sentence(3),
            OrderingProvider = $"Dr. {_faker.Name.LastName()}",
            Device = _faker.PickRandom(devices),
            Liters = _faker.Random.Bool() ? $"{_faker.Random.Int(1, 10)} L" : null,
            Usage = _faker.Random.Bool() ? string.Join(" ", _faker.Lorem.Words(3)) : null
        };
    }

    private static (string Text, DeviceOrder Expected) BuildTextAndExpected(this PhysicianNoteBuilder builder)
    {
        return (builder.BuildNoteText(), builder.BuildExpectedOrder());
    }
}