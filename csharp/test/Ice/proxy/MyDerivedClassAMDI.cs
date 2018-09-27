// **********************************************************************
//
// Copyright (c) 2003-2018 ZeroC, Inc. All rights reserved.
//
// This copy of Ice is licensed to you under the terms described in the
// ICE_LICENSE file included in this distribution.
//
// **********************************************************************

using System.Threading.Tasks;
using System.Collections.Generic;

namespace Ice
{
    namespace proxy
    {
        namespace AMD
        {
            public sealed class MyDerivedClassI : Test.MyDerivedClassDisp_
            {
                public MyDerivedClassI()
                {
                }

                public override Task<Ice.ObjectPrx> echoAsync(Ice.ObjectPrx obj, Ice.Current c)
                {
                    return Task.FromResult(obj);
                }

                public override Task shutdownAsync(Ice.Current current)
                {
                    current.adapter.getCommunicator().shutdown();
                    return null;
                }

                public override Task<Dictionary<string, string>> getContextAsync(Ice.Current current)
                {
                    return Task.FromResult(_ctx);
                }

                public override bool ice_isA(string s, Ice.Current current)
                {
                    _ctx = current.ctx;
                    return base.ice_isA(s, current);
                }

                private Dictionary<string, string> _ctx;
            }
        }
    }
}
