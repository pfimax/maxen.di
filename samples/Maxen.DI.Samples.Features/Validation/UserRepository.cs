namespace Maxen.DI.Samples.Features.Validation;

internal sealed class UserRepository
{
    public UserRepository(Database database) => Database = database;

    public Database Database { get; }
}
