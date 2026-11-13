using Maxen.DI.Samples.Generators;
using Maxen.DI;
using Maxen.DI.Samples.Generators.Generic;
using Maxen.DI.Samples.Generators.Interception;
using Maxen.DI.Samples.Generators.Keyed;
using Maxen.DI.Samples.Generators.Plain;
using Maxen.DI.Samples.Generators.Pricing;

// Everything below is wired from attributes. The only builder calls are the one that applies the generated
// registrations, and the registrations the generator cannot see (the open generic service and Plain).
var builder = new ContainerBuilder();
builder.RegisterGeneratedServices();                       // [Service], [Decorator] and [Intercept] classes
builder.Register(typeof(IShelf<>), typeof(Shelf<>));       // open generic service; its decorator is an attribute
builder.Register<Plain>();                                 // plain registration: gets a generated activator

await using var container = builder.Build();
using var scope = container.CreateScope();

// Decorators: StandardPricing, wrapped by DiscountPricing, wrapped by TaxPricing (ordered by Priority).
var price = scope.Resolve<IPriceCalculator>().PriceFor(100m);
Console.WriteLine($"price of 100 after discount and tax: {price}");

// Interception: two generated interceptors ran around the call.
var count = scope.Resolve<IInventory>().Count("widgets");
Console.WriteLine($"inventory count: {count}, intercepted calls: {CallCounter.Calls}, audit trail: {string.Join(",", AuditTrail.Entries)}");
Console.WriteLine($"service type is a generated proxy: {scope.Resolve<IInventory>().GetType().Name}");

// Keyed attribute registrations.
Console.WriteLine($"keyed: {scope.Resolve<IStorage>("disk").Where} and {scope.Resolve<IStorage>("cloud").Where}");

// Open generic decorator.
Console.WriteLine($"open generic decorator: {scope.Resolve<IShelf<string>>().Label()} {scope.Resolve<IShelf<int>>().Label()}");

Console.WriteLine(scope.Resolve<Plain>().Hello());

// A generated registration is a factory, which validation can't look into. The generator writes down what each one
// resolves, so the [Service] classes are checked, and drawn in the graph, like services registered by type.
Console.WriteLine($"validation issues: {container.Validate().Count}, dependencies in the graph: {container.BuildGraph().Edges.Count}");
