using dotOneChain.Api.Models;
using System.Collections.Concurrent;

namespace dotOneChain.Api.Services;

public interface IMempool { void Enqueue(Tx1155 tx); List<Tx1155> Drain(int max = 1000); int Count { get; } }

public class Mempool : IMempool
{
    private readonly ConcurrentQueue<Tx1155> _queue = new();
    public int Count => _queue.Count;
    public void Enqueue(Tx1155 tx) => _queue.Enqueue(tx);
    public List<Tx1155> Drain(int max = 1000)
    {
        var list = new List<Tx1155>();
        while (list.Count < max && _queue.TryDequeue(out var tx)) list.Add(tx);
        return list;
    }
}
