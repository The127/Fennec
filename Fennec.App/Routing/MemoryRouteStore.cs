using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fennec.App.Routing;

public class MemoryRouteStore : IRouteStore
{
    private record HistoryEntry(IRoute Route, ObservableObject? ViewModel);

    private readonly int _maxCached;
    private readonly int _maxHistory;

    private readonly LinkedList<HistoryEntry> _ancientHistory = new();
    private readonly LinkedList<HistoryEntry> _backwardsStack = new();
    private HistoryEntry? _current;
    private readonly LinkedList<HistoryEntry> _forwardsStack = new();
    private readonly LinkedList<HistoryEntry> _farFuture = new();

    public MemoryRouteStore(int maxCached, int maxHistory)
    {
        if (maxCached < 0) throw new ArgumentOutOfRangeException(nameof(maxCached), "maxCached must be non-negative");
        if (maxHistory < maxCached)
            throw new ArgumentOutOfRangeException(nameof(maxHistory), "maxHistory must be at least maxCached");

        _maxCached = maxCached;
        _maxHistory = maxHistory;
    }

    public Task<ObservableObject> PushAsync(IRoute route, CancellationToken cancellationToken = default)
    {
        if (_current is not null)
        {
            _backwardsStack.AddLast(_current);
            if (_backwardsStack.Count > _maxCached)
            {
                var historyEntry = _backwardsStack.First!.Value with { ViewModel = null };
                _backwardsStack.RemoveFirst();

                _ancientHistory.AddLast(historyEntry);
                if (_ancientHistory.Count + _backwardsStack.Count > _maxHistory)
                {
                    _ancientHistory.RemoveFirst();
                }
            }
        }

        _forwardsStack.Clear();
        _farFuture.Clear();

        _current = new HistoryEntry(route, route.GetViewModel());
        return Task.FromResult(_current.ViewModel!);
    }

    public Task<ObservableObject?> GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (_backwardsStack.Count == 0 && _ancientHistory.Count == 0)
        {
            return Task.FromResult<ObservableObject?>(null);
        }

        if (_current is not null)
        {
            _forwardsStack.AddFirst(_current);

            if (_forwardsStack.Count > _maxCached)
            {
                var historyEntry = _forwardsStack.Last!.Value with { ViewModel = null };
                _forwardsStack.RemoveLast();

                _farFuture.AddFirst(historyEntry);
            }
        }

        if (_backwardsStack.Count == 0)
        {
            var historyEntry = _ancientHistory.Last!.Value;
            _ancientHistory.RemoveLast();

            _current = historyEntry with { ViewModel = historyEntry.Route.GetViewModel() };
        }
        else
        {
            _current = _backwardsStack.Last!.Value;
            _backwardsStack.RemoveLast();
        }

        return Task.FromResult(_current.ViewModel);
    }

    public Task<ObservableObject?> GoForwardAsync(CancellationToken cancellationToken = default)
    {
        if (_forwardsStack.Count == 0 && _farFuture.Count == 0)
        {
            return Task.FromResult<ObservableObject?>(null);
        }

        if (_current is null)
        {
            throw new UnreachableException("how the helly? we have forwards but no current!");
        }
        
        _backwardsStack.AddLast(_current);
        if (_backwardsStack.Count > _maxCached)
        {
            var historyEntry = _backwardsStack.First!.Value with { ViewModel = null };
            _backwardsStack.RemoveFirst();

            _ancientHistory.AddLast(historyEntry);
            if (_ancientHistory.Count + _backwardsStack.Count > _maxHistory)
            {
                _ancientHistory.RemoveFirst();
            }
        }

        if (_forwardsStack.Count == 0)
        {
            var historyEntry = _farFuture.First!.Value;
            _farFuture.RemoveFirst();
            
            _current = historyEntry with { ViewModel = historyEntry.Route.GetViewModel() };
        }
        else
        {
            _current = _forwardsStack.First!.Value;
            _forwardsStack.RemoveFirst();
        }
        
        return Task.FromResult(_current.ViewModel);
    }
}