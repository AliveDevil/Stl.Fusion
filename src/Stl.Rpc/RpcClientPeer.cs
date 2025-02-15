using Stl.Rpc.Internal;

namespace Stl.Rpc;

public class RpcClientPeer : RpcPeer
{
    private volatile AsyncState<Moment> _reconnectAt = new(default, true);

    public RpcClientConnectionFactory ConnectionFactory { get; init; }
    public RpcClientPeerReconnectDelayer ReconnectDelayer { get; init; }

    public AsyncState<Moment> ReconnectsAt => _reconnectAt;

    public RpcClientPeer(RpcHub hub, RpcPeerRef @ref)
        : base(hub, @ref)
    {
        LocalServiceFilter = static _ => false;
        ConnectionFactory = Hub.ClientConnectionFactory;
        ReconnectDelayer = Hub.ClientPeerReconnectDelayer;
    }

    // Protected methods

    protected override async Task<RpcConnection> GetConnection(CancellationToken cancellationToken)
    {
        var connectionState = ConnectionState.Last.Value;
        if (connectionState.IsConnected())
            throw Stl.Internal.Errors.InternalError(
                $"{nameof(RpcClient)}.{nameof(GetConnection)}() is called, but the peer is already connected.");

        var delay = ReconnectDelayer.GetDelay(this, connectionState.TryIndex, connectionState.Error, cancellationToken);
        if (delay.IsLimitExceeded)
            throw Errors.ConnectionUnrecoverable();

        SetReconnectsAt(delay.EndsAt);
        try {
            await delay.Task.ConfigureAwait(false);
        }
        finally {
            SetReconnectsAt(default);
        }

        Log.LogInformation("'{PeerRef}': Connecting...", Ref);
        return await ConnectionFactory.Invoke(this, cancellationToken).ConfigureAwait(false);
    }

    protected void SetReconnectsAt(Moment value)
    {
        lock (Lock) {
            if (_reconnectAt.Value != value)
                _reconnectAt = _reconnectAt.SetNext(value);
        }
    }
}
