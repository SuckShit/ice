// **********************************************************************
//
// Copyright (c) 2003-present ZeroC, Inc. All rights reserved.
//
// **********************************************************************

package test.Ice.location;

public class Client extends test.TestHelper
{
    public void run(String[] args)
    {
        Ice.Properties properties = createTestProperties(args);
        properties.setProperty("Ice.Package.Test", "test.Ice.location");
        properties.setProperty("Ice.Default.Locator", "locator:" + getTestEndpoint(properties, 0));
        try(Ice.Communicator communicator = initialize(properties))
        {
            AllTests.allTests(this);
        }
        catch(Exception ex)
        {
            throw new RuntimeException(ex);
        }
    }
}
