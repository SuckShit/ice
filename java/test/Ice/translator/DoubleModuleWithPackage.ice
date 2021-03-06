//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

// dmwp = double module with package

[[java:package(dmwp)]]

module M1::M2
{

enum dmwpEnum { dmwpE1, dmwpE2 }

const dmwpEnum dmwpConstant = dmwpE2;

struct dmwpStruct
{
    dmwpEnum e;
}

sequence<dmwpStruct> dmwpStructSeq;

dictionary<string, dmwpStruct> dmwpStringStructDict;

interface dmwpBaseInterface
{
    void dmwpBaseInterfaceOp();
}

interface dmwpInterface : dmwpBaseInterface
{
    void dmwpInterfaceOp();
}

class dmwpBaseClass
{
    dmwpEnum e;
    dmwpStruct s;
    dmwpStructSeq seq;
    dmwpStringStructDict dict;
}

class dmwpClass : dmwpBaseClass implements dmwpInterface
{
}

exception dmwpBaseException
{
    dmwpEnum e;
    dmwpStruct s;
    dmwpStructSeq seq;
    dmwpStringStructDict dict;
    dmwpClass c;
}

exception dmwpException : dmwpBaseException
{
}

}
