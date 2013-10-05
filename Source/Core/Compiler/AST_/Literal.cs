/*

 Copyright (c) 2013 DEVSENSE

 The use and distribution terms for this software are contained in the file named License.txt, 
 which can be found in the root of the Phalanger distribution. By using this software 
 in any fashion, you are agreeing to be bound by the terms of this license.
 
 You must not remove this notice from this software.

*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Diagnostics;

using PHP.Core.AST;
using PHP.Core.Emit;
using PHP.Core.Parsers;
using PHP.Core.Reflection;

namespace PHP.Core.Compiler.AST
{
    internal static class LiteralUtils
    {
        public static Literal/*!*/Create(Position position, object value, AccessType access)
        {
            Literal result;

            if (value == null) result = new NullLiteral(position);
            else if (value.GetType() == typeof(int)) result = new IntLiteral(position, (int)value);
            else if (value.GetType() == typeof(string)) result = new StringLiteral(position, (string)value);
            else if (value.GetType() == typeof(bool)) result = new BoolLiteral(position, (bool)value);
            else if (value.GetType() == typeof(double)) result = new DoubleLiteral(position, (double)value);
            else if (value.GetType() == typeof(long)) result = new LongIntLiteral(position, (long)value);
            else if (value.GetType() == typeof(PhpBytes)) result = new BinaryStringLiteral(position, (PhpBytes)value);
            else throw new ArgumentException("value");

            //
            Debug.Assert(result != null);
            result.NodeCompiler<IExpressionCompiler>().Access = access;

            //
            return result;
        }
    }

    partial class NodeCompilers
    {
        [NodeCompiler(typeof(IntLiteral), Singleton = true)]
        [NodeCompiler(typeof(LongIntLiteral), Singleton = true)]
        [NodeCompiler(typeof(DoubleLiteral), Singleton = true)]
        [NodeCompiler(typeof(StringLiteral), Singleton = true)]
        [NodeCompiler(typeof(BinaryStringLiteral), Singleton = true)]
        [NodeCompiler(typeof(BoolLiteral), Singleton = true)]
        [NodeCompiler(typeof(NullLiteral), Singleton = true)]
        sealed class LiteralCompiler : ExpressionCompiler<Literal>
        {
            public override Evaluation EvaluatePriorAnalysis(Literal node, SourceUnit sourceUnit)
            {
                return new Evaluation(node, node.Value);
            }

            public override Evaluation Analyze(Literal node, Analyzer analyzer, ExInfoFromParent info)
            {
                // possible access values: Read, None
                access = info.Access;
                return new Evaluation(node, node.Value);
            }

            public override bool IsDeeplyCopied(Literal node, CopyReason reason, int nestingLevel)
            {
                return false;
            }

            /// <summary>
            /// Emits the literal. The common code for all literals.
            /// </summary>
            public override PhpTypeCode Emit(Literal node, CodeGenerator codeGenerator)
            {
                ILEmitter il = codeGenerator.IL;

                // loads the value:
                il.LoadLiteral(node.Value);

                switch (access)
                {
                    case AccessType.Read:
                        return node.ValueTypeCode;

                    case AccessType.None:
                        il.Emit(OpCodes.Pop);
                        return node.ValueTypeCode;

                    case AccessType.ReadUnknown:
                    case AccessType.ReadRef:
                        // created by evaluation a function called on literal, e.g. $x =& sin(10);
                        codeGenerator.EmitBoxing(node.ValueTypeCode);
                        il.Emit(OpCodes.Newobj, Constructors.PhpReference_Object);

                        return PhpTypeCode.PhpReference;
                }

                Debug.Fail("Invalid access type");
                return PhpTypeCode.Invalid;
            }
        }
    }
}