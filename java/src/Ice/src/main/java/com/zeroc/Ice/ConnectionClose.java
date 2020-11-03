//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

package com.zeroc.Ice;

/**
 * Determines the behavior when manually closing a connection.
 **/
public enum ConnectionClose
{
    /**
     * Close the connection immediately without sending a close connection protocol message to the peer
     * and waiting for the peer to acknowledge it.
     **/
    Forcefully(0),
    /**
     * Close the connection by notifying the peer but do not wait for pending outgoing invocations to complete.
     * On the server side, the connection will not be closed until all incoming invocations have completed.
     **/
    Gracefully(1),
    /**
     * Wait for all pending invocations to complete before closing the connection.
     **/
    GracefullyWithWait(2);

    public int value()
    {
        return _value;
    }

    public static ConnectionClose valueOf(int v)
    {
        switch(v)
        {
        case 0:
            return Forcefully;
        case 1:
            return Gracefully;
        case 2:
            return GracefullyWithWait;
        }
        return null;
    }

    private ConnectionClose(int v)
    {
        _value = v;
    }

    private final int _value;
}
