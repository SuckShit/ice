// Copyright (c) ZeroC, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroC.Ice.Discovery
{
    /// <summary>Servant class that implements the Slice interface Ice::Locator.</summary>
    internal class Locator : IAsyncLocator
    {
        private readonly string _domainId;
        private readonly int _latencyMultiplier;

        private readonly ILookupPrx _lookup;

        // The key is a single-endpoint datagram Lookup proxy extracted from the _lookup proxy.
        // The value is a dummy datagram proxy with usually a single endpoint that is one of _replyAdapter's endpoints
        // and that matches the interface of the key's endpoint.
        private readonly Dictionary<ILookupPrx, IObjectPrx> _lookups = new ();

        private readonly string _pluginName;

        private readonly ObjectAdapter _replyAdapter;

        private readonly ILocatorRegistryPrx _registry;
        private readonly int _retryCount;
        private readonly TimeSpan _timeout;

        public async ValueTask<IObjectPrx?> FindAdapterByIdAsync(
            string adapterId,
            Current current,
            CancellationToken cancel)
        {
            using var replyServant = new FindAdapterByIdReply(_replyAdapter);
            return await InvokeAsync(
                (lookup, dummyReply) =>
                {
                    IFindAdapterByIdReplyPrx reply =
                        dummyReply.Clone(IFindAdapterByIdReplyPrx.Factory, identity: replyServant.Identity);

                    return lookup.FindAdapterByIdAsync(_domainId,
                                                      adapterId,
                                                      reply,
                                                      cancel: cancel);
                },
                replyServant).ConfigureAwait(false);
        }

        public async ValueTask<IObjectPrx?> FindObjectByIdAsync(
            Identity identity,
            string? facet,
            Current current,
            CancellationToken cancel)
        {
            using var replyServant = new FindObjectByIdReply(_replyAdapter);
            return await InvokeAsync(
                (lookup, dummyReply) =>
                {
                    IFindObjectByIdReplyPrx reply =
                        dummyReply.Clone(IFindObjectByIdReplyPrx.Factory, identity: replyServant.Identity);

                    return lookup.FindObjectByIdAsync(_domainId, identity, facet, reply, cancel: cancel);
                },
                replyServant).ConfigureAwait(false);
        }

        public ValueTask<ILocatorRegistryPrx?> GetRegistryAsync(Current current, CancellationToken cancel) =>
            new (_registry);

        public async ValueTask<(IEnumerable<EndpointData>, IEnumerable<string>)> ResolveLocationAsync(
            string[] location,
            Current current,
            CancellationToken cancel)
        {
            if (location.Length == 0)
            {
                throw new InvalidArgumentException("location cannot be empty", nameof(location));
            }

            string adapterId = location[0];

            using var replyServant = new ResolveAdapterIdReply(_replyAdapter);

            IReadOnlyList<EndpointData> endpoints = await InvokeAsync(
                (lookup, dummyReply) =>
                {
                    IResolveAdapterIdReplyPrx reply =
                        dummyReply.Clone(IResolveAdapterIdReplyPrx.Factory, identity: replyServant.Identity);

                    return lookup.ResolveAdapterIdAsync(_domainId,
                                                        adapterId,
                                                        reply,
                                                        cancel: cancel);
                },
                replyServant).ConfigureAwait(false);

            if (endpoints.Count > 0)
            {
                return (endpoints, location[1..]);
            }
            else
            {
                return (endpoints, ImmutableArray<string>.Empty);
            }
        }

        public async ValueTask<(IEnumerable<EndpointData>, IEnumerable<string>)> ResolveWellKnownProxyAsync(
            Identity identity,
            string facet,
            Current current,
            CancellationToken cancel)
        {
            using var replyServant = new ResolveWellKnownProxyReply(_replyAdapter);

            string adapterId = await InvokeAsync(
                (lookup, dummyReply) =>
                {
                    IResolveWellKnownProxyReplyPrx reply =
                            dummyReply.Clone(IResolveWellKnownProxyReplyPrx.Factory, identity: replyServant.Identity);

                    return lookup.ResolveWellKnownProxyAsync(_domainId,
                                                             identity,
                                                             facet,
                                                             reply,
                                                             cancel: cancel);
                },
                replyServant).ConfigureAwait(false);

            // We never return endpoints
            return (ImmutableArray<EndpointData>.Empty,
                    adapterId.Length > 0 ? ImmutableArray.Create(adapterId) : ImmutableArray<string>.Empty);
        }

        internal Locator(ILocatorRegistryPrx registry, ILookupPrx lookup, ObjectAdapter replyAdapter, string pluginName)
        {
            _pluginName = pluginName;
            _registry = registry;
            _replyAdapter = replyAdapter;

            Communicator communicator = replyAdapter.Communicator;

            _timeout = communicator.GetPropertyAsTimeSpan($"{_pluginName}.Timeout") ?? TimeSpan.FromMilliseconds(300);
            if (_timeout == Timeout.InfiniteTimeSpan)
            {
                _timeout = TimeSpan.FromMilliseconds(300);
            }
            _lookup = lookup.Clone(invocationTimeout: _timeout);

            _retryCount = communicator.GetPropertyAsInt($"{_pluginName}.RetryCount") ?? 3;

            _latencyMultiplier = communicator.GetPropertyAsInt($"{_pluginName}.LatencyMultiplier") ?? 1;
            if (_latencyMultiplier < 1)
            {
                throw new InvalidConfigurationException(
                    $"the value of `{_pluginName}.LatencyMultiplier' must be an integer greater than 0");
            }

            _domainId = communicator.GetProperty($"{_pluginName}.DomainId") ?? "";

            // Create one lookup proxy per endpoint from the given proxy. We want to send a multicast datagram on each
            // of the lookup proxy.

            // Dummy proxy for replies which can have multiple endpoints (but see below).
            IObjectPrx lookupReply = _replyAdapter.CreateProxy("dummy", IObjectPrx.Factory);
            Debug.Assert(lookupReply.InvocationMode == InvocationMode.Datagram);

            foreach (Endpoint endpoint in _lookup.Endpoints)
            {
                if (!endpoint.IsDatagram)
                {
                    throw new InvalidConfigurationException($"{_pluginName}.Lookup can only have udp endpoints");
                }

                ILookupPrx key = _lookup.Clone(endpoints: ImmutableArray.Create(endpoint));
                if (endpoint["interface"] is string mcastInterface && mcastInterface.Length > 0)
                {
                    Endpoint? q = lookupReply.Endpoints.FirstOrDefault(e => e.Host == mcastInterface);
                    if (q != null)
                    {
                        _lookups[key] = lookupReply.Clone(endpoints: ImmutableArray.Create(q));
                    }
                }

                if (!_lookups.ContainsKey(key))
                {
                    // Fallback: just use the given lookup reply proxy if no matching endpoint found.
                    _lookups[key] = lookupReply;
                }
            }
            Debug.Assert(_lookups.Count > 0);
        }

        /// <summary>Invokes a find or resolve request on a Lookup object and processes the reply(ies).</summary>
        /// <param name="findAsync">A delegate that performs the remote call. Its parameters correspond to an entry in
        /// the _lookups dictionary.</param>
        /// <param name="replyServant">The reply servant.</param>
        private async Task<TResult> InvokeAsync<TResult>(
            Func<ILookupPrx, IObjectPrx, Task> findAsync,
            ReplyServant<TResult> replyServant)
        {
            // We retry only when at least one findAsync request is sent successfully and we don't get any reply.
            // TODO: this _retryCount is really an attempt count not a retry count.
            for (int i = 0; i < _retryCount; ++i)
            {
                TimeSpan start = Time.Elapsed;

                var timeoutTask = Task.Delay(_timeout, replyServant.CancellationToken);

                var sendTask = Task.WhenAll(_lookups.Select(
                    entry =>
                    {
                        try
                        {
                            return findAsync(entry.Key, entry.Value);
                        }
                        catch (Exception ex)
                        {
                            return Task.FromException(ex);
                        }
                    }));

                Task task = await Task.WhenAny(sendTask, replyServant.Task, timeoutTask).ConfigureAwait(false);

                if (task == sendTask)
                {
                    if (sendTask.Status == TaskStatus.Faulted)
                    {
                        if (sendTask.Exception!.InnerExceptions.Count == _lookups.Count)
                        {
                            // All the tasks failed: log warning and return empty result (no retry)
                            _replyAdapter.Communicator.Logger.Warning(
                                @$"{_pluginName} failed to send lookup request using `{_lookup
                                    }':\n{sendTask.Exception!.InnerException!}");
                            replyServant.SetEmptyResult();
                            return await replyServant.Task.ConfigureAwait(false);
                        }
                    }
                    // For Canceled or RanToCompletion, we assume at least one send was successful. If we're wrong,
                    // we'll timeout soon anyways.

                    task = await Task.WhenAny(replyServant.Task, timeoutTask).ConfigureAwait(false);
                }

                if (task == replyServant.Task)
                {
                    return await replyServant.Task.ConfigureAwait(false);
                }
                else if (task.IsCanceled)
                {
                    // If the timeout was canceled we delay the completion of the request to give a chance to other
                    // members of this replica group to reply
                    return await
                        replyServant.WaitForReplicaGroupRepliesAsync(start, _latencyMultiplier).ConfigureAwait(false);
                }
                // else timeout, so we retry until _retryCount
            }

            replyServant.SetEmptyResult(); // _retryCount exceeded
            return await replyServant.Task.ConfigureAwait(false);
        }
    }

    /// <summary>The base class of all Reply servant that helps collect / gather the reply(ies) to a lookup reques.
    /// </summary>
    internal class ReplyServant<TResult> : IObject, IDisposable
    {
        internal CancellationToken CancellationToken => _cancellationSource.Token;
        internal Identity Identity { get; }

        internal Task<TResult> Task => _completionSource.Task;

        private readonly CancellationTokenSource _cancellationSource;
        private readonly TaskCompletionSource<TResult> _completionSource;
        private readonly TResult _emptyResult;

        private readonly ObjectAdapter _replyAdapter;

        public void Dispose()
        {
            _cancellationSource.Dispose();
            _replyAdapter.Remove(Identity);
        }

        internal void SetEmptyResult() => _completionSource.SetResult(_emptyResult);

        internal async Task<TResult> WaitForReplicaGroupRepliesAsync(TimeSpan start, int latencyMultiplier)
        {
            // This method is called by InvokeAsync after the first reply from a replica group to wait for additional
            // replies from the replica group.
            TimeSpan latency = (Time.Elapsed - start) * latencyMultiplier;
            if (latency == TimeSpan.Zero)
            {
                latency = TimeSpan.FromMilliseconds(1);
            }
            await System.Threading.Tasks.Task.Delay(latency).ConfigureAwait(false);

            SetResult(CollectReplicaReplies());
            return await Task.ConfigureAwait(false);
        }

        private protected ReplyServant(TResult emptyResult, ObjectAdapter replyAdapter)
        {
            // Add servant (this) to object adapter with new UUID identity.
            Identity = replyAdapter.AddWithUUID(this, IObjectPrx.Factory).Identity;

            _cancellationSource = new ();
            _completionSource = new ();
            _emptyResult = emptyResult;
            _replyAdapter = replyAdapter;
        }

        private protected void Cancel() => _cancellationSource.Cancel();

        private protected virtual TResult CollectReplicaReplies()
        {
            Debug.Assert(false); // must be overridden if called by WaitForReplicaGroupRepliesAsync
            return _emptyResult;
        }

        private protected void SetResult(TResult result) => _completionSource.SetResult(result);
    }

    /// <summary>Servant class that implements the Slice interface FindAdapterByIdReply.</summary>
    internal sealed class FindAdapterByIdReply : ReplyServant<IObjectPrx?>, IFindAdapterByIdReply
    {
        private readonly object _mutex = new ();
        private readonly HashSet<IObjectPrx> _proxies = new ();

        public void FoundAdapterById(
            string adapterId,
            IObjectPrx proxy,
            bool isReplicaGroup,
            Current current,
            CancellationToken cancel)
        {
            if (isReplicaGroup)
            {
                lock (_mutex)
                {
                    _proxies.Add(proxy);
                    if (_proxies.Count == 1)
                    {
                        // Cancel WhenAny and let InvokeAsync wait for additional replies from the replica group, and
                        // later call CollectReplicaReplies.
                        Cancel();
                    }
                }
            }
            else
            {
                SetResult(proxy);
            }
        }

        internal FindAdapterByIdReply(ObjectAdapter replyAdapter)
            : base(emptyResult: null, replyAdapter)
        {
        }

        private protected override IObjectPrx? CollectReplicaReplies()
        {
            lock (_mutex)
            {
                Debug.Assert(_proxies.Count > 0);
                var endpoints = new List<Endpoint>();
                IObjectPrx result = _proxies.First();
                foreach (IObjectPrx prx in _proxies)
                {
                    endpoints.AddRange(prx.Endpoints);
                }
                return result.Clone(endpoints: endpoints);
            }
        }
    }

    /// <summary>Servant class that implements the Slice interface FindObjectByIdReply.</summary>
    internal class FindObjectByIdReply : ReplyServant<IObjectPrx?>, IFindObjectByIdReply
    {
        public void FoundObjectById(Identity id, IObjectPrx proxy, Current current, CancellationToken cancel) =>
            SetResult(proxy);

        internal FindObjectByIdReply(ObjectAdapter replyAdapter)
            : base(emptyResult: null, replyAdapter)
        {
        }
    }

    /// <summary>Servant class that implements the Slice interface ResolveAdapterIdReply.</summary>
    internal sealed class ResolveAdapterIdReply : ReplyServant<IReadOnlyList<EndpointData>>, IResolveAdapterIdReply
    {
        private readonly object _mutex = new ();
        private readonly HashSet<EndpointData> _endpointDataSet = new (EndpointDataComparer.Instance);

        public void FoundAdapterId(
            EndpointData[] endpoints,
            bool isReplicaGroup,
            Current current,
            CancellationToken cancel)
        {
            if (isReplicaGroup)
            {
                lock (_mutex)
                {
                    bool firstReply = _endpointDataSet.Count == 0;

                    _endpointDataSet.UnionWith(endpoints);
                    if (firstReply)
                    {
                        Cancel();
                    }
                }
            }
            else
            {
                SetResult(endpoints);
            }
        }

        internal ResolveAdapterIdReply(ObjectAdapter replyAdapter)
            : base(ImmutableArray<EndpointData>.Empty, replyAdapter)
        {
        }

        private protected override IReadOnlyList<EndpointData> CollectReplicaReplies()
        {
            lock (_mutex)
            {
                Debug.Assert(_endpointDataSet.Count > 0);
                return _endpointDataSet.ToList();
            }
        }
    }

    /// <summary>Servant class that implements the Slice interface ResolveWellKnownProxyReply.</summary>
    internal class ResolveWellKnownProxyReply : ReplyServant<string>, IResolveWellKnownProxyReply
    {
        public void FoundWellKnownProxy(string adapterId, Current current, CancellationToken cancel) =>
            SetResult(adapterId);

        internal ResolveWellKnownProxyReply(ObjectAdapter replyAdapter)
            : base(emptyResult: "", replyAdapter)
        {
        }
    }

    // Temporary helper class
    internal sealed class EndpointDataComparer : IEqualityComparer<EndpointData>
    {
        internal static readonly EndpointDataComparer Instance = new ();

        public bool Equals(EndpointData x, EndpointData y) =>
            x.Transport == y.Transport &&
            x.Host == y.Host &&
            x.Port == y.Port &&
            x.Options.SequenceEqual(y.Options);

        public int GetHashCode(EndpointData obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Transport);
            hash.Add(obj.Host);
            hash.Add(obj.Port);
            foreach (string s in obj.Options)
            {
                hash.Add(s);
            }
            return hash.ToHashCode();
        }
    }
}
