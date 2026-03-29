using System.Collections.Concurrent;
using Bussy.Net.Registries;
using Bussy.Net.Transport;
using Moq;

namespace Bussy.Net.Test;

[TestFixture]
public sealed class TransportRegistryTests
{
    private TransportRegistry _subject = null!;

    [SetUp]
    public void Setup()
    {
        _subject = new TransportRegistry();
    }

    [Test]
    public void Constructor_InitializesEmptyTransports()
    {
        Assert.That(_subject.Transports, Is.Empty);
    }

    [Test]
    public void Register_AddsTransportToRegistry()
    {
        var transport = CreateTransportMock("in-memory");

        _subject.Register(transport.Object);

        Assert.That(_subject.Transports, Has.Count.EqualTo(1));
        Assert.That(_subject.Transports, Does.ContainKey("in-memory"));
    }

    [Test]
    public void Register_StoresTransportByName()
    {
        var transport = CreateTransportMock("rabbitmq");

        _subject.Register(transport.Object);

        Assert.That(_subject.Transports["rabbitmq"], Is.SameAs(transport.Object));
    }

    [Test]
    public void Register_WithMultipleTransports_StoresAllTransports()
    {
        var inMemory = CreateTransportMock("in-memory");
        var rabbitmq = CreateTransportMock("rabbitmq");
        var kafka = CreateTransportMock("kafka");

        _subject.Register(inMemory.Object);
        _subject.Register(rabbitmq.Object);
        _subject.Register(kafka.Object);

        Assert.That(_subject.Transports, Has.Count.EqualTo(3));
        Assert.That(_subject.Transports, Does.ContainKey("in-memory"));
        Assert.That(_subject.Transports, Does.ContainKey("rabbitmq"));
        Assert.That(_subject.Transports, Does.ContainKey("kafka"));
    }

    [Test]
    public void Register_WithDuplicateName_KeepsFirstRegistration()
    {
        var firstTransport = CreateTransportMock("rabbitmq");
        var secondTransport = CreateTransportMock("rabbitmq");

        _subject.Register(firstTransport.Object);
        _subject.Register(secondTransport.Object);

        Assert.That(_subject.Transports, Has.Count.EqualTo(1));
        Assert.That(_subject.Transports["rabbitmq"], Is.SameAs(firstTransport.Object));
    }

    [Test]
    public void Register_WithCaseSensitiveName_StoresSeparately()
    {
        var lowercase = CreateTransportMock("rabbitmq");
        var uppercase = CreateTransportMock("RABBITMQ");

        _subject.Register(lowercase.Object);
        _subject.Register(uppercase.Object);

        Assert.That(_subject.Transports, Has.Count.EqualTo(2));
        Assert.That(_subject.Transports["rabbitmq"], Is.SameAs(lowercase.Object));
        Assert.That(_subject.Transports["RABBITMQ"], Is.SameAs(uppercase.Object));
    }

    [Test]
    public void Register_WithSpecialCharactersInName_StoresTransport()
    {
        var transport = CreateTransportMock("my-transport.v1");

        _subject.Register(transport.Object);

        Assert.That(_subject.Transports, Does.ContainKey("my-transport.v1"));
    }

    [Test]
    public void Transports_ReturnsNotNull()
    {
        Assert.That(_subject.Transports, Is.Not.Null);
    }

    [Test]
    public void Transports_IsConcurrentDictionary()
    {
        Assert.That(_subject.Transports, Is.TypeOf<ConcurrentDictionary<string, ITransport>>());
    }

    [Test]
    public void Transports_CanBeIteratedAfterRegistration()
    {
        var rabbitmq = CreateTransportMock("rabbitmq");
        var kafka = CreateTransportMock("kafka");

        _subject.Register(rabbitmq.Object);
        _subject.Register(kafka.Object);

        var keys = _subject.Transports.Keys.ToList();
        Assert.That(keys, Has.Count.EqualTo(2));
        Assert.That(keys, Does.Contain("rabbitmq"));
        Assert.That(keys, Does.Contain("kafka"));
    }

    [Test]
    public void Register_MultipleCallsWithSameName_FirstRegistrationWins()
    {
        var firstTransport = CreateTransportMock("sqs");
        var secondTransport = CreateTransportMock("sqs");
        var thirdTransport = CreateTransportMock("sqs");

        _subject.Register(firstTransport.Object);
        var result1 = _subject.Transports["sqs"];
        
        _subject.Register(secondTransport.Object);
        var result2 = _subject.Transports["sqs"];
        
        _subject.Register(thirdTransport.Object);
        var result3 = _subject.Transports["sqs"];

        Assert.That(result1, Is.SameAs(firstTransport.Object));
        Assert.That(result2, Is.SameAs(firstTransport.Object));
        Assert.That(result3, Is.SameAs(firstTransport.Object));
    }

    [Test]
    public void Register_WithNullTransport_ThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() => _subject.Register(null!));
    }

    [Test]
    public void Transports_AllowsDirectAccess()
    {
        var transport = CreateTransportMock("in-memory");
        _subject.Register(transport.Object);

        var retrieved = _subject.Transports["in-memory"];

        Assert.That(retrieved, Is.SameAs(transport.Object));
    }

    [Test]
    public void Transports_Values_ContainsAllRegisteredTransports()
    {
        var rabbitmq = CreateTransportMock("rabbitmq");
        var kafka = CreateTransportMock("kafka");

        _subject.Register(rabbitmq.Object);
        _subject.Register(kafka.Object);

        var values = _subject.Transports.Values.ToList();
        Assert.That(values, Has.Count.EqualTo(2));
        Assert.That(values, Does.Contain(rabbitmq.Object));
        Assert.That(values, Does.Contain(kafka.Object));
    }

    private static Mock<ITransport> CreateTransportMock(string name)
    {
        var mock = new Mock<ITransport>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Capabilities).Returns(TransportCapability.None);
        return mock;
    }
}



