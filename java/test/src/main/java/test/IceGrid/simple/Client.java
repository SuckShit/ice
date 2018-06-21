// **********************************************************************
//
// Copyright (c) 2003-2018 ZeroC, Inc. All rights reserved.
//
// This copy of Ice is licensed to you under the terms described in the
// ICE_LICENSE file included in this distribution.
//
// **********************************************************************

package test.IceGrid.simple;

import java.util.stream.Stream;

public class Client extends test.TestHelper
{
    public void run(String[] args)
    {
        com.zeroc.Ice.Properties properties = createTestProperties(args);
        properties.setProperty("Ice.Package.Test", "test.IceGrid.simple");
        try(com.zeroc.Ice.Communicator communicator = initialize(properties))
        {
            boolean withDeploy =  Stream.of(args).anyMatch(v -> v.equals("--with-deploy"));

            if(!withDeploy)
            {
                AllTests.allTests(this);
            }
            else
            {
                AllTests.allTestsWithDeploy(this);
            }
        }
    }
}
