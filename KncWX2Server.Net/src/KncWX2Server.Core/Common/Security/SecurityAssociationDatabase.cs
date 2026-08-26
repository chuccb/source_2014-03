namespace KncWX2Server.Core.Common.Security;

/// <summary>Managed counterpart of the legacy KSADatabase.</summary>
public sealed class SecurityAssociationDatabase
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ushort, SecurityAssociation> _associations = new()
    {
        [0] = new(),
    };

    public void Insert(out ushort spi, SecurityAssociation association)
    {
        ArgumentNullException.ThrowIfNull(association);

        lock (_gate)
        {
            do
            {
                spi = checked((ushort)Random.Shared.Next(1, ushort.MaxValue + 1));
            }
            while (_associations.ContainsKey(spi));

            _associations[spi] = association;
        }
    }

    public void Delete(ushort spi)
    {
        lock (_gate)
            _associations.Remove(spi);
    }

    public bool Find(ushort spi)
    {
        lock (_gate)
            return _associations.ContainsKey(spi);
    }

    public SecurityAssociation Get(ushort spi)
    {
        lock (_gate)
            return _associations.TryGetValue(spi, out var association)
                ? association
                : _associations[0];
    }

    public SecurityAssociation CreateNew(out ushort spi)
    {
        var association = new SecurityAssociation();
        association.ResetRandomizeKey();
        Insert(out spi, association);
        return association;
    }
}
