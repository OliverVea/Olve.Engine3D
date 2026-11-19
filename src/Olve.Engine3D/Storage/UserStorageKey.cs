using Olve.Utilities.Ids;

namespace Olve.Engine3D.Storage;

public readonly record struct UserStorageKey(Id<UserStorageArea> Area, Id<UserStorageEntry> Entry)
{
    public override string ToString() => $"{Area}/{Entry}";
}
