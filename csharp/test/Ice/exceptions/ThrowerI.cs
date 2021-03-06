// Copyright (c) ZeroC, Inc. All rights reserved.

using System;
using System.Threading;
using Test;

namespace ZeroC.Ice.Test.Exceptions
{
    public sealed class Thrower : IThrower
    {
        public void Shutdown(Current current, CancellationToken cancel) =>
            current.Adapter.Communicator.ShutdownAsync();

        public bool SupportsAssertException(Current current, CancellationToken cancel) => false;

        public void ThrowAasA(int a, Current current, CancellationToken cancel) => throw new A(a);

        public void ThrowAorDasAorD(int a, Current current, CancellationToken cancel)
        {
            if (a > 0)
            {
                throw new A(a);
            }
            else
            {
                throw new D(a);
            }
        }

        public void ThrowBasA(int a, int b, Current current, CancellationToken cancel) => ThrowBasB(a, b, current, cancel);

        public void ThrowBasB(int a, int b, Current current, CancellationToken cancel) => throw new B(a, b);

        public void ThrowCasA(int a, int b, int c, Current current, CancellationToken cancel) =>
            ThrowCasC(a, b, c, current, cancel);

        public void ThrowCasB(int a, int b, int c, Current current, CancellationToken cancel) =>
            ThrowCasC(a, b, c, current, cancel);

        public void ThrowCasC(int a, int b, int c, Current current, CancellationToken cancel) => throw new C(a, b, c);

        public void ThrowLocalException(Current current, CancellationToken cancel) =>
            throw new ConnectionClosedException();

        public void ThrowNonIceException(Current current, CancellationToken cancel) => throw new Exception();

        public void ThrowAssertException(Current current, CancellationToken cancel) => TestHelper.Assert(false);

        // 20KB is over the configured 10KB message size max.
        public ReadOnlyMemory<byte> ThrowMemoryLimitException(byte[] seq, Current current, CancellationToken cancel) =>
            new byte[1024 * 20];

        public void ThrowLocalExceptionIdempotent(Current current, CancellationToken cancel) =>
            throw new ConnectionClosedException();

        public void ThrowAfterResponse(Current current, CancellationToken cancel)
        {
            // Only relevant for AMD.
        }

        // Only relevant for AMD.
        public void ThrowAfterException(Current current, CancellationToken cancel) => throw new A();

        public void ThrowAConvertedToUnhandled(Current current, CancellationToken cancel) =>
            throw new A() { ConvertToUnhandled = true };
    }
}
