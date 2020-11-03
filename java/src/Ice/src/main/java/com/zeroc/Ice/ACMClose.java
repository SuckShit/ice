//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

package com.zeroc.Ice;

/**
 * Specifies the close semantics for Active Connection Management.
 **/
public enum ACMClose
{
    /**
     * Disables automatic connection closure.
     **/
    CloseOff(0),
    /**
     * Gracefully closes a connection that has been idle for the configured timeout period.
     **/
    CloseOnIdle(1),
    /**
     * Forcefully closes a connection that has been idle for the configured timeout period,
     * but only if the connection has pending invocations.
     **/
    CloseOnInvocation(2),
    /**
     * Combines the behaviors of CloseOnIdle and CloseOnInvocation.
     **/
    CloseOnInvocationAndIdle(3),
    /**
     * Forcefully closes a connection that has been idle for the configured timeout period,
     * regardless of whether the connection has pending invocations or dispatch.
     **/
    CloseOnIdleForceful(4);

    public int value()
    {
        return _value;
    }

    public static ACMClose valueOf(int v)
    {
        switch(v)
        {
        case 0:
            return CloseOff;
        case 1:
            return CloseOnIdle;
        case 2:
            return CloseOnInvocation;
        case 3:
            return CloseOnInvocationAndIdle;
        case 4:
            return CloseOnIdleForceful;
        }
        return null;
    }

    private ACMClose(int v)
    {
        _value = v;
    }

    private final int _value;
}
