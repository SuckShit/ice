// **********************************************************************
//
// Copyright (c) 2001
// MutableRealms, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

package IceInternal;

public class IncomingConnectionFactory extends EventHandler
{
    public synchronized void
    hold()
    {
        setState(StateHolding);
    }

    public synchronized void
    activate()
    {
        setState(StateActive);
    }

    public Endpoint
    endpoint()
    {
        // No mutex protection necessary, _endpoint is immutable.
        return _endpoint;
    }

    public boolean
    equivalent(Endpoint endp)
    {
        if (_transceiver != null)
        {
            return endp.equivalent(_transceiver);
        }

        assert(_acceptor != null);
        return endp.equivalent(_acceptor);
    }

    public synchronized Connection[]
    connections()
    {
        //
        // Reap destroyed connections.
        //
        java.util.ListIterator iter = _connections.listIterator();
        while (iter.hasNext())
        {
            Connection connection = (Connection)iter.next();
            if (connection.destroyed())
            {
                iter.remove();
            }
        }

        Connection[] arr = new Connection[_connections.size()];
        _connections.toArray(arr);
        return arr;
    }

    //
    // Operations from EventHandler.
    //
    public boolean
    readable()
    {
        return false;
    }

    public boolean
    tryRead(BasicStream unused)
    {
        assert(false); // Must not be called.
        return false;
    }

    public void
    read(BasicStream unused)
    {
        assert(false); // Must not be called.
    }

    public synchronized void
    message(BasicStream unused, ThreadPool threadPool)
    {
        threadPool.promoteFollower();

        if (_state != StateActive)
        {
            Thread.yield();
            return;
        }

        //
        // Reap destroyed connections.
        //
        java.util.ListIterator iter = _connections.listIterator();
        while (iter.hasNext())
        {
            Connection connection = (Connection)iter.next();
            if (connection.destroyed())
            {
                iter.remove();
            }
        }

        //
        // Now accept a new connection.
        //
        try
        {
            Transceiver transceiver = _acceptor.accept(0);
            Connection connection = new Connection(_instance, transceiver, _endpoint, _adapter);
            connection.activate();
            _connections.add(connection);
        }
        catch (Ice.TimeoutException ex)
        {
            // Ignore timeouts.
        }
        catch (Ice.LocalException ex)
        {
            if (_warn)
            {
                warning(ex);
            }
            setState(StateClosed);
        }
    }

    public synchronized void
    finished(ThreadPool threadPool)
    {
        threadPool.promoteFollower();

        if (_state == StateActive)
        {
            registerWithPool();
        }
        else if (_state == StateClosed)
        {
            try
            {
                //
                // Clear listen() backlog properly by accepting all queued
                // connections, and then shutting them down.
                //
                while (true)
                {
                    try
                    {
                        Transceiver transceiver = _acceptor.accept(0);
                        Connection connection = new Connection(_instance, transceiver, _endpoint, _adapter);
                        connection.exception(new Ice.ObjectAdapterDeactivatedException());
                    }
                    catch (Ice.TimeoutException ex)
                    {
                        break; // Exit loop on timeout.
                    }
                }
            }
            catch (Ice.LocalException ex)
            {
                if (_warn)
                {
                    warning(ex);
                }
            }

            _acceptor.close();
        }
    }

    public void
    exception(Ice.LocalException ex)
    {
        assert(false); // Must not be called.
    }

    /*
    public boolean
    tryDestroy(ThreadPool threadPool)
    {
        //
        // Do nothing. We don't want collector factories to be closed by
        // active connection management.
        //
        return false;
    }
    */

    public
    IncomingConnectionFactory(Instance instance, Endpoint endpoint,
                              Ice.ObjectAdapter adapter)
    {
        super(instance);
        _endpoint = endpoint;
	DefaultsAndOverwrites defaultsAndOverwrites = _instance.defaultsAndOverwrites();
	if (defaultsAndOverwrites.overwriteTimeout)
	{
	    _endpoint = _endpoint.timeout(defaultsAndOverwrites.overwriteTimeoutValue);
	}
        _adapter = adapter;
        _state = StateHolding;
	_warn = _instance.properties().getPropertyAsInt("Ice.ConnectionWarnings") > 0 ? true : false;

        try
        {
            EndpointHolder h = new EndpointHolder();
            h.value = _endpoint;
            _transceiver = _endpoint.serverTransceiver(h);
            if (_transceiver != null)
            {
                _endpoint = h.value;
                Connection connection = new Connection(_instance, _transceiver, _endpoint, _adapter);
                _connections.add(connection);
            }
            else
            {
                h.value = _endpoint;
                _acceptor = _endpoint.acceptor(h);
                _endpoint = h.value;
                assert(_acceptor != null);
                _acceptor.listen();
            }
        }
        catch (RuntimeException ex)
        {
            setState(StateClosed);
            throw ex;
        }
    }

    protected void
    finalize()
        throws Throwable
    {
        assert(_state == StateClosed);

        //
        // Destroy the EventHandler's stream, so that its buffer
        // can be reclaimed.
        //
        super._stream.destroy();

        super.finalize();
    }

    public synchronized void
    destroy()
    {
        setState(StateClosed);
    }

    private static final int StateActive = 0;
    private static final int StateHolding = 1;
    private static final int StateClosed = 2;

    private void
    setState(int state)
    {
        if (_state == state) // Don't switch twice.
        {
            return;
        }

        switch (state)
        {
            case StateActive:
            {
                if (_state != StateHolding) // Can only switch from holding to active.
                {
                    return;
                }
                registerWithPool();

                java.util.ListIterator iter = _connections.listIterator();
                while (iter.hasNext())
                {
                    Connection connection = (Connection)iter.next();
                    connection.activate();
                }
                break;
            }

            case StateHolding:
            {
                if (_state != StateActive) // Can only switch from active to holding
                {
                    return;
                }
                unregisterWithPool();

                java.util.ListIterator iter = _connections.listIterator();
                while (iter.hasNext())
                {
                    Connection connection = (Connection)iter.next();
                    connection.hold();
                }
                break;
            }

            case StateClosed:
            {
                //
                // If we come from holding state, we first need to
                // register again before we unregister.
                //
                if (_state == StateHolding)
                {
                    registerWithPool();
                }
                unregisterWithPool();

                java.util.ListIterator iter = _connections.listIterator();
                while (iter.hasNext())
                {   
                    Connection connection = (Connection)iter.next();
                    connection.destroy(Connection.ObjectAdapterDeactivated);
                }
                _connections.clear();

                break;
            }
        }

        _state = state;
    }

    private void
    registerWithPool()
    {
        if (_acceptor != null)
        {
            if (_serverThreadPool == null)
            {
                _serverThreadPool = _instance.serverThreadPool();
                assert(_serverThreadPool != null);
            }
            _serverThreadPool._register(_acceptor.fd(), this);
        }
    }

    private void
    unregisterWithPool()
    {
        if (_acceptor != null)
        {
            assert(_serverThreadPool != null);
            _serverThreadPool.unregister(_acceptor.fd());
        }
    }

    private void
    warning(Ice.LocalException ex)
    {
        java.io.StringWriter sw = new java.io.StringWriter();
        java.io.PrintWriter pw = new java.io.PrintWriter(sw);
        ex.printStackTrace(pw);
        pw.flush();
        String s = "connection exception:\n" + sw.toString() + '\n' + _acceptor.toString();
        _instance.logger().warning(s);
    }

    private Endpoint _endpoint;
    private Ice.ObjectAdapter _adapter;
    private Acceptor _acceptor;
    private Transceiver _transceiver;
    private ThreadPool _serverThreadPool;
    private java.util.LinkedList _connections = new java.util.LinkedList();
    private int _state;
    private boolean _warn;
}
