using Stl.Rpc;

namespace Stl.Fusion.Extensions;

public sealed class RpcPeerStateMonitor : WorkerBase
{
    private readonly IMutableState<RpcPeerState?> _state;

    private IServiceProvider Services { get; }

    public RpcPeerRef PeerRef { get; set; } = RpcPeerRef.Default;
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromSeconds(1);
    public IState<RpcPeerState?> State => _state;

    public RpcPeerStateMonitor(IServiceProvider services)
    {
        Services = services;
        _state = services.StateFactory().NewMutable((RpcPeerState?)null, $"{GetType().Name}.{nameof(State)}");
    }

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        await Task.Delay(StartDelay, cancellationToken).ConfigureAwait(false);
        try {
            var peer = Services.RpcHub().GetPeer(PeerRef);
            var clientPeer = peer as RpcClientPeer;
            // This delay gives some time for peer to connect
            using var cts = cancellationToken.LinkWith(peer.StopToken);
            await foreach (var e in peer.ConnectionState.Events(cts.Token).ConfigureAwait(false)) {
                var connectionState = e.Value;
                var isConnected = connectionState.IsConnected();
                var nextState = new RpcPeerState(isConnected, connectionState.Error);

                _state.Value = nextState;
                if (clientPeer != null && !isConnected) {
                    // Client peer disconnected, we need to watch for ReconnectAt value change now
                    for (var i = 0; i < 10; i++) {
                        // ReSharper disable once MethodSupportsCancellation
                        if (e.WhenNext().IsCompleted)
                            break;
                        if (clientPeer.ReconnectsAt is { } vReconnectsAt) {
                            nextState = nextState with { ReconnectsAt = vReconnectsAt };
                            _state.Value = nextState;
                            break;
                        }

                        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            _state.Value = null;
        }
        catch (Exception) {
            _state.Value = null;
            if (cancellationToken.IsCancellationRequested)
                throw;
        }
    }
}
