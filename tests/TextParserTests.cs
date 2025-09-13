using SignalBooster.Services;
using SignalBooster.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Configuration;
using NSubstitute;
using Xunit;

namespace SignalBooster.Tests;

[Trait("Category", "Unit")]
public class TextParserTests
{
    private readonly TextParser _parser;

    public TextParserTests()
    {
        var logger = Substitute.For<ILogger<TextParser>>();
        var options = Substitute.For<IOptions<SignalBoosterOptions>>();
        options.Value.Returns(new SignalBoosterOptions { OpenAI = new OpenAIOptions() });
        
        _parser = new TextParser(options, logger);
    }

    [Fact]
    public void ParseDeviceOrder_CpapNote_ReturnsCpapOrder()
    {
        var note = "Patient needs a CPAP with full face mask and humidifier. AHI > 20. Ordered by Dr. Cameron.";
        
        var result = _parser.ParseDeviceOrder(note);
        
        Assert.Equal("CPAP", result.Device);
        Assert.Equal("full face", result.MaskType);
        Assert.Contains("humidifier", result.AddOns);
        Assert.Equal("AHI > 20", result.Qualifier);
        Assert.Equal("Dr. Cameron", result.OrderingProvider);
    }

    [Fact]
    public void ParseDeviceOrder_OxygenNote_ReturnsOxygenOrder()
    {
        var note = @"Patient Name: Harold Finch
                     DOB: 04/12/1952
                     Diagnosis: COPD
                     Patient needs oxygen 2 L for sleep and exertion. 
                     Ordered by Dr. Cuddy.";
        
        var result = _parser.ParseDeviceOrder(note);
        
        Assert.Equal("Oxygen Tank", result.Device);
        Assert.Equal("2 L", result.Liters);
        Assert.Equal("sleep and exertion", result.Usage);
        Assert.Equal("Harold Finch", result.PatientName);
        Assert.Equal("04/12/1952", result.Dob);
        Assert.Equal("COPD", result.Diagnosis);
        Assert.Equal("Dr. Cuddy", result.OrderingProvider);
    }

    [Fact]
    public void ParseDeviceOrder_UnknownDevice_ReturnsUnknown()
    {
        var note = "Patient needs some medical device.";
        
        var result = _parser.ParseDeviceOrder(note);
        
        Assert.Equal("Unknown", result.Device);
    }

    [Fact]
    public void ParseDeviceOrder_BiPAPDevice_ReturnsBiPAP()
    {
        var note = "Patient needs BiPAP therapy for sleep apnea.";
        
        var result = _parser.ParseDeviceOrder(note);
        
        Assert.Equal("BiPAP", result.Device);
    }

    [Fact]
    public void ParseDeviceOrder_WalkerDevice_ReturnsWalker()
    {
        var note = "Patient needs a rollator walker for mobility.";
        
        var result = _parser.ParseDeviceOrder(note);
        
        Assert.Equal("Walker", result.Device);
    }

    [Fact]
    public void ParseDeviceOrder_NebulizerDevice_ReturnsNebulizer()
    {
        var note = "Patient needs nebulizer for breathing treatment.";
        
        var result = _parser.ParseDeviceOrder(note);
        
        Assert.Equal("Nebulizer", result.Device);
    }
}