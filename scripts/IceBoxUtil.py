# **********************************************************************
#
# Copyright (c) 2003-2018 ZeroC, Inc. All rights reserved.
#
# This copy of Ice is licensed to you under the terms described in the
# ICE_LICENSE file included in this distribution.
#
# **********************************************************************

import sys, os
from Util import *

class IceBox(ProcessFromBinDir, Server):

    def __init__(self, configFile=None, *args, **kargs):
        Server.__init__(self, *args, **kargs)
        self.configFile = configFile

    def setup(self, current):
        mapping = self.getMapping(current)

        #
        # If running IceBox tests with .NET Core we need to generate a config
        # file that use the service for the .NET Framework used to build the
        # tests
        #
        if self.configFile:
            if isinstance(mapping, CSharpMapping) and current.config.dotnetcore:
                configFile = self.configFile.format(testdir=current.testsuite.getPath())
                with open(configFile, 'r') as source:
                    framework = mapping.getLibTargetFramework(current)
                    newConfigFile = configFile + ".netcoreapp"
                    with open(newConfigFile, 'w') as target:
                        for line in source.readlines():
                            target.write(line.replace("\\net45\\", "\\netstandard2.0\\{0}\\".format(framework)))
                        current.files.append(newConfigFile)

    def getExe(self, current):
        mapping = self.getMapping(current)
        if isinstance(mapping, JavaCompatMapping):
            return "IceBox.Server"
        elif isinstance(mapping, JavaMapping):
            return "com.zeroc.IceBox.Server"
        elif isinstance(mapping, CSharpMapping):
            return "iceboxnet"
        else:
            name = "icebox"
            if isinstance(platform, Linux) and \
               platform.getLinuxId() in ["centos", "rhel", "fedora"] and \
               current.config.buildPlatform == "x86":
                name += "32" # Multilib platform
            if current.config.cpp11:
                name += "++11"
            return name

    def getEffectiveArgs(self, current, args):
        args = Server.getEffectiveArgs(self, current, args)
        if self.configFile:
            mapping = self.getMapping(current)
            if isinstance(mapping, CSharpMapping) and current.config.dotnetcore:
                args.append("--Ice.Config={0}".format(self.configFile + ".netcoreapp"))
            else:
                args.append("--Ice.Config={0}".format(self.configFile))
        return args

class IceBoxAdmin(ProcessFromBinDir, ProcessIsReleaseOnly, Client):

    def getMapping(self, current):
        # IceBox admin is only provided with the C++/Java, not C#
        mapping = Client.getMapping(self, current)
        if isinstance(mapping, CppMapping) or isinstance(mapping, JavaMapping):
            return mapping
        else:
            return Mapping.getByName("cpp")

    def getExe(self, current):
        mapping = self.getMapping(current)
        if isinstance(mapping, JavaCompatMapping):
            return "IceBox.Admin"
        elif isinstance(mapping, JavaMapping):
            return "com.zeroc.IceBox.Admin"
        else:
            return "iceboxadmin"
