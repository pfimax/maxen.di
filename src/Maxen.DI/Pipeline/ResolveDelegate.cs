namespace Maxen.DI.Pipeline;

/// <summary>One step of the resolution pipeline: turns a context into the service instance.</summary>
public delegate object ResolveDelegate(ResolveContext context);
