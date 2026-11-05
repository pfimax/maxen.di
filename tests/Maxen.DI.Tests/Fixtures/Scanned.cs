namespace Maxen.DI.Tests.Fixtures;

/// <summary>Registers itself through the <see cref="ServiceAttribute"/>.</summary>
[Service(ServiceType = typeof(IScanned))]
public class Scanned : IScanned { }
