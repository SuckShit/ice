//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

#ifndef GEN_H
#define GEN_H

#include <Slice/Parser.h>
#include "JavaUtil.h"

namespace Slice
{

class JavaVisitor : public JavaGenerator, public ParserVisitor
{
public:

    virtual ~JavaVisitor();

protected:

    JavaVisitor(const std::string&);

    enum ParamDir { InParam, OutParam };

    std::string getResultType(const OperationPtr&, const std::string&, bool, bool);
    void writeResultType(::IceUtilInternal::Output&, const OperationPtr&, const std::string&, const CommentPtr&);
    void writeMarshaledResultType(::IceUtilInternal::Output&, const OperationPtr&, const std::string&,
                                  const CommentPtr&);

    void allocatePatcher(::IceUtilInternal::Output&, const TypePtr&, const std::string&, const std::string&, bool);
    std::string getPatcher(const TypePtr&, const std::string&, const std::string&);

    std::string getFutureType(const OperationPtr&, const std::string&);
    std::string getFutureImplType(const OperationPtr&, const std::string&);

    //
    // Compose the parameter lists for an operation.
    //
    std::vector<std::string> getParams(const OperationPtr&, const std::string&);
    std::vector<std::string> getParamsProxy(const OperationPtr&, const std::string&, bool = false);

    //
    // Compose the argument lists for an operation.
    //
    std::vector<std::string> getArgs(const OperationPtr&);
    std::vector<std::string> getInArgs(const OperationPtr&, bool = false);

    void writeMarshalProxyParams(::IceUtilInternal::Output&, const std::string&, const OperationPtr&);
    void writeUnmarshalProxyResults(::IceUtilInternal::Output&, const std::string&, const OperationPtr&);
    void writeMarshalServantResults(::IceUtilInternal::Output&, const std::string&, const OperationPtr&,
                                    const std::string&);

    //
    // Generate a throws clause containing only checked exceptions.
    // op is provided only when we want to check for the java:UserException metadata
    //
    void writeThrowsClause(const std::string&, const ExceptionList&, const OperationPtr& op = 0);

    //
    // Generate code to compute a hash code for a type.
    //
    void writeHashCode(::IceUtilInternal::Output&, const TypePtr&, const std::string&, int&,
                       const std::list<std::string>& = std::list<std::string>());

    //
    // Marshal/unmarshal a data member.
    //
    void writeMarshalDataMember(::IceUtilInternal::Output&, const std::string&, const MemberPtr&, int&, bool = false);
    void writeUnmarshalDataMember(::IceUtilInternal::Output&, const std::string&, const MemberPtr&, int&, bool = false);

    //
    // Generate dispatch methods for an interface.
    //
    void writeDispatch(::IceUtilInternal::Output&, const InterfaceDefPtr&);

    //
    // Generate marshaling methods for a class.
    //
    void writeMarshaling(::IceUtilInternal::Output&, const ClassDefPtr&);

    //
    // Write a constant or default value initializer.
    //
    void writeConstantValue(::IceUtilInternal::Output&, const TypePtr&, const SyntaxTreeBasePtr&, const std::string&,
                            const std::string&);

    //
    // Generate assignment statements for those data members that have default values.
    //
    void writeDataMemberInitializers(::IceUtilInternal::Output&, const MemberList&, const std::string&);

    //
    // Handle doc comments.
    //
    static StringList splitComment(const ContainedPtr&);
    void writeHiddenDocComment(::IceUtilInternal::Output&);
    void writeDocCommentLines(::IceUtilInternal::Output&, const StringList&);
    void writeDocCommentLines(::IceUtilInternal::Output&, const std::string&);
    void writeDocComment(::IceUtilInternal::Output&, const UnitPtr&, const CommentPtr&);
    void writeDocComment(::IceUtilInternal::Output&, const std::string&);
    void writeProxyDocComment(::IceUtilInternal::Output&, const OperationPtr&, const std::string&, const CommentPtr&,
                              bool, const std::string&);
    void writeHiddenProxyDocComment(::IceUtilInternal::Output&, const OperationPtr&);
    void writeServantDocComment(::IceUtilInternal::Output&, const OperationPtr&, const std::string&,
                                const CommentPtr&, bool);
    void writeSeeAlso(::IceUtilInternal::Output&, const UnitPtr&, const std::string&);
};

class Gen : private ::IceUtil::noncopyable
{
public:

    Gen(const std::string&,
        const std::string&,
        const std::vector<std::string>&,
        const std::string&);
    ~Gen();

    void generate(const UnitPtr&);
    void generateImpl(const UnitPtr&);

private:

    std::string _base;
    std::vector<std::string> _includePaths;
    std::string _dir;

    class PackageVisitor : public JavaVisitor
    {
    public:

        PackageVisitor(const std::string&);

        bool visitModuleStart(const ModulePtr&) override;
    };

    class TypesVisitor : public JavaVisitor
    {
    public:

        TypesVisitor(const std::string&);

        bool visitClassDefStart(const ClassDefPtr&) override;
        void visitClassDefEnd(const ClassDefPtr&) override;
        bool visitInterfaceDefStart(const InterfaceDefPtr&) override;
        void visitInterfaceDefEnd(const InterfaceDefPtr&) override;
        void visitOperation(const OperationPtr&) override;
        bool visitExceptionStart(const ExceptionPtr&) override;
        void visitExceptionEnd(const ExceptionPtr&) override;
        bool visitStructStart(const StructPtr&) override;
        void visitStructEnd(const StructPtr&) override;
        void visitDataMember(const MemberPtr&) override;
        void visitEnum(const EnumPtr&) override;
        void visitConst(const ConstPtr&) override;
    };

    class CompactIdVisitor : public JavaVisitor
    {
    public:

        CompactIdVisitor(const std::string&);

        bool visitClassDefStart(const ClassDefPtr&) override;
    };

    class HelperVisitor : public JavaVisitor
    {
    public:

        HelperVisitor(const std::string&);

        void visitSequence(const SequencePtr&) override;
        void visitDictionary(const DictionaryPtr&) override;
    };

    class ProxyVisitor : public JavaVisitor
    {
    public:

        ProxyVisitor(const std::string&);

        bool visitInterfaceDefStart(const InterfaceDefPtr&) override;
        void visitInterfaceDefEnd(const InterfaceDefPtr&) override;
        void visitOperation(const OperationPtr&) override;
    };

    class ImplVisitor : public JavaVisitor
    {
    public:

        ImplVisitor(const std::string&);

        bool visitInterfaceDefStart(const InterfaceDefPtr&) override;

    protected:

        //
        // Returns a default value for the type.
        //
        std::string getDefaultValue(const std::string&, const TypePtr&);

        //
        // Generate code to initialize the operation result.
        //
        bool initResult(::IceUtilInternal::Output&, const std::string&, const OperationPtr&);

        //
        // Generate an operation.
        //
        void writeOperation(::IceUtilInternal::Output&, const std::string&, const OperationPtr&);
    };
};

}

#endif
