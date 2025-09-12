using SignalBooster.Models;

namespace SignalBooster.Tests.TestHelpers;

public class PhysicianNoteBuilder
{
    private string _patientName = "John Doe";
    private string _dateOfBirth = "01/01/1980";
    private string _diagnosis = "Generic condition";
    private string _orderingProvider = "Dr. Smith";
    private string _device = "CPAP";
    private string? _liters;
    private string? _usage;
    private string? _maskType;
    private List<string>? _addOns;
    private string? _qualifier;

    public static PhysicianNoteBuilder Create() => new();

    public PhysicianNoteBuilder WithPatient(string name, string dob)
    {
        _patientName = name;
        _dateOfBirth = dob;
        return this;
    }

    public PhysicianNoteBuilder WithDiagnosis(string diagnosis)
    {
        _diagnosis = diagnosis;
        return this;
    }

    public PhysicianNoteBuilder WithProvider(string provider)
    {
        _orderingProvider = provider;
        return this;
    }

    public PhysicianNoteBuilder WithDevice(string device)
    {
        _device = device;
        return this;
    }

    public PhysicianNoteBuilder WithOxygenSpecs(string liters, string usage)
    {
        _liters = liters;
        _usage = usage;
        return this;
    }

    public PhysicianNoteBuilder WithCpapSpecs(string maskType, params string[] addOns)
    {
        _maskType = maskType;
        _addOns = addOns.ToList();
        return this;
    }

    public PhysicianNoteBuilder WithQualifier(string qualifier)
    {
        _qualifier = qualifier;
        return this;
    }

    public string BuildNoteText()
    {
        var note = $"""
            Patient Name: {_patientName}
            DOB: {_dateOfBirth}
            Diagnosis: {_diagnosis}
            Ordering Physician: {_orderingProvider}

            """;

        note += _device switch
        {
            "Oxygen Tank" when _liters != null && _usage != null =>
                $"Patient requires oxygen tank with {_liters} flow rate for {_usage}.",
            
            "CPAP" when _maskType != null =>
                $"Patient requires CPAP machine with {_maskType} mask" +
                (_addOns?.Any() == true ? $" with {string.Join(" and ", _addOns)}" : "") +
                (_qualifier != null ? $". {_qualifier}." : "."),
            
            "CPAP" => "Patient requires CPAP machine for sleep apnea.",
            
            "Hospital Bed" when _addOns?.Any() == true =>
                $"Patient requires hospital bed with {string.Join(" and ", _addOns)}" +
                (_qualifier != null ? $" due to {_qualifier}" : "") + ".",
            
            "TENS Unit" => $"Patient requires TENS unit for pain management.",
            
            _ => $"Patient requires {_device.ToLower()}."
        };

        return note;
    }

    public DeviceOrder BuildExpectedOrder()
    {
        return new DeviceOrder
        {
            PatientName = _patientName,
            Dob = _dateOfBirth,
            Diagnosis = _diagnosis,
            OrderingProvider = _orderingProvider,
            Device = _device,
            Liters = _liters,
            Usage = _usage,
            MaskType = _maskType,
            AddOns = _addOns?.ToArray(),
            Qualifier = _qualifier
        };
    }
}