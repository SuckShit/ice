// **********************************************************************
//
// Copyright (c) 2003-present ZeroC, Inc. All rights reserved.
//
// **********************************************************************

const Ice = require("../Ice/ModuleRegistry").Ice;
Ice.AsyncStatus = {Queued: 0, Sent: 1};
module.exports.Ice = Ice;
