using System.Collections.Concurrent;

namespace ClipMate.Core.Events;

/// <summary>
/// Provides loosely-coupled publish/subscribe event messaging between components.
/// </summary>
public class EventAggregator : IEventAggregator
{
    private readonly ConcurrentDictionary<Type, List<WeakReference>> _subscriptions = new();

    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="eventMessage">The event message to publish.</param>
    public void Publish<TEvent>(TEvent eventMessage) where TEvent : class
    {
        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var subscribers))
        {
            return;
        }

        // Clean up dead references and invoke active subscribers
        var deadReferences = new List<WeakReference>();

        foreach (var weakRef in subscribers.ToList())
        {
            if (!weakRef.IsAlive)
            {
                deadReferences.Add(weakRef);
                continue;
            }

            if (weakRef.Target is Action<TEvent> handler)
            {
                handler(eventMessage);
            }
        }

        // Remove dead references
        foreach (var deadRef in deadReferences)
        {
            subscribers.Remove(deadRef);
        }
    }

    /// <summary>
    /// Publishes an event asynchronously to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to publish.</typeparam>
    /// <param name="eventMessage">The event message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default) 
        where TEvent : class
    {
        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var subscribers))
        {
            return;
        }

        var deadReferences = new List<WeakReference>();
        var tasks = new List<Task>();

        foreach (var weakRef in subscribers.ToList())
        {
            if (!weakRef.IsAlive)
            {
                deadReferences.Add(weakRef);
                continue;
            }

            if (weakRef.Target is Func<TEvent, Task> asyncHandler)
            {
                tasks.Add(asyncHandler(eventMessage));
            }
            else if (weakRef.Target is Action<TEvent> handler)
            {
                tasks.Add(Task.Run(() => handler(eventMessage), cancellationToken));
            }
        }

        // Remove dead references
        foreach (var deadRef in deadReferences)
        {
            subscribers.Remove(deadRef);
        }

        // Wait for all handlers to complete
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <returns>A subscription token that can be disposed to unsubscribe.</returns>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var subscribers = _subscriptions.GetOrAdd(eventType, _ => new List<WeakReference>());
        var weakRef = new WeakReference(handler);

        lock (subscribers)
        {
            subscribers.Add(weakRef);
        }

        return new Subscription(() => Unsubscribe(eventType, weakRef));
    }

    /// <summary>
    /// Subscribes to events of a specific type with an async handler.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <param name="handler">The async handler to invoke when the event is published.</param>
    /// <returns>A subscription token that can be disposed to unsubscribe.</returns>
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        var eventType = typeof(TEvent);
        var subscribers = _subscriptions.GetOrAdd(eventType, _ => new List<WeakReference>());
        var weakRef = new WeakReference(handler);

        lock (subscribers)
        {
            subscribers.Add(weakRef);
        }

        return new Subscription(() => Unsubscribe(eventType, weakRef));
    }

    /// <summary>
    /// Unsubscribes a handler from an event type.
    /// </summary>
    private void Unsubscribe(Type eventType, WeakReference weakRef)
    {
        if (_subscriptions.TryGetValue(eventType, out var subscribers))
        {
            lock (subscribers)
            {
                subscribers.Remove(weakRef);
            }
        }
    }

    /// <summary>
    /// Clears all subscriptions.
    /// </summary>
    public void ClearAll()
    {
        _subscriptions.Clear();
    }

    /// <summary>
    /// Represents a subscription that can be disposed to unsubscribe.
    /// </summary>
    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _unsubscribe();
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for the event aggregator.
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    void Publish<TEvent>(TEvent eventMessage) where TEvent : class;

    /// <summary>
    /// Publishes an event asynchronously to all subscribers.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent eventMessage, CancellationToken cancellationToken = default) where TEvent : class;

    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>
    /// Subscribes to events of a specific type with an async handler.
    /// </summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class;

    /// <summary>
    /// Clears all subscriptions.
    /// </summary>
    void ClearAll();
}
