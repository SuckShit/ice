// **********************************************************************
//
// Copyright (c) 2003-2007 ZeroC, Inc. All rights reserved.
//
// This copy of Ice is licensed to you under the terms described in the
// ICE_LICENSE file included in this distribution.
//
// **********************************************************************

// Generated by makeprops.py from file XXX- stuff needs to go here!

// IMPORTANT: Do not edit this file -- any edits made here will be lost!

namespace IceInternal
{
    public sealed class Property
    {
        public Property(string pattern, bool deprecated, string deprecatedBy)
        {
            _pattern = pattern;
            _deprecated = deprecated;
            _deprecatedBy = deprecatedBy;
        }

        public string
        pattern()
        {
            return _pattern;
        }

        public bool
        deprecated()
        {
            return _deprecated;
        }

        public string
        deprecatedBy()
        {
            return _deprecatedBy;
        }

        private string _pattern;
        private bool _deprecated;
        private string _deprecatedBy;
    }
}
