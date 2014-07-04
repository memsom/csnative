﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LlvmHelpersGen.cs" company="">
//   
// </copyright>
// <summary>
//   
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Il2Native.Logic.Gencode
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using Il2Native.Logic.CodeParts;
    using Il2Native.Logic.Exceptions;

    using PEAssemblyReader;

    /// <summary>
    /// </summary>
    public static class LlvmHelpersGen
    {
        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="type">
        /// </param>
        public static void WriteAlloca(this LlvmWriter llvmWriter, IType type)
        {
            var writer = llvmWriter.Output;

            // for value types
            writer.Write("alloca ");
            type.WriteTypePrefix(writer);
            writer.Write(", align " + LlvmWriter.PointerSize);
        }

        public static void WriteBitcast(this LlvmWriter llvmWriter, OpCodePart opCode, IType toType)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, toType);
            writer.Write("bitcast ");
            opCode.Result.Type.WriteTypePrefix(writer, true);
            writer.Write(" ");
            llvmWriter.WriteResultNumber(opCode.Result);
            writer.Write(" to ");
            toType.WriteTypePrefix(writer, true);
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCode">
        /// </param>
        /// <param name="result">
        /// </param>
        /// <param name="toType">
        /// </param>
        public static void WriteBitcast(this LlvmWriter llvmWriter, OpCodePart opCode, LlvmResult result, IType toType)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, toType);
            writer.Write("bitcast ");
            result.Type.WriteTypePrefix(writer, true);
            writer.Write(" ");
            llvmWriter.WriteResultNumber(result);
            writer.Write(" to ");
            toType.WriteTypePrefix(writer, true);
        }

        public static void WriteBitcast(this LlvmWriter llvmWriter, OpCodePart opCode, FullyDefinedReference source, IType toType)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, toType);
            writer.Write("bitcast ");
            source.Type.WriteTypePrefix(writer, true);
            writer.Write(" ");
            llvmWriter.WriteResultNumber(source);
            writer.Write(" to ");
            toType.WriteTypePrefix(writer, true);
        }

        public static void WriteIntToPtr(this LlvmWriter llvmWriter, OpCodePart opCode, FullyDefinedReference source, IType toType)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, toType);
            writer.Write("inttoptr ");
            source.Type.WriteTypePrefix(writer);
            writer.Write(" ");
            llvmWriter.WriteResultNumber(source);
            writer.Write(" to ");
            toType.WriteTypePrefix(writer, true);
        }

        public static void WriteBitcast(this LlvmWriter llvmWriter, OpCodePart opCode, LlvmResult result)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, llvmWriter.ResolveType("System.Byte").CreatePointer());
            writer.Write("bitcast ");
            result.Type.WriteTypePrefix(writer, true);
            writer.Write(" ");
            llvmWriter.WriteResultNumber(result);
            writer.Write(" to i8*");
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCode">
        /// </param>
        /// <param name="fromType">
        /// </param>
        /// <param name="name">
        /// </param>
        public static void WriteBitcast(this LlvmWriter llvmWriter, OpCodePart opCode, IType fromType, string name)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, llvmWriter.ResolveType("System.Byte").CreatePointer());
            writer.Write("bitcast ");
            fromType.WriteTypePrefix(writer, true);
            writer.Write(" ");
            writer.Write(name);
            writer.Write(" to i8*");
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCode">
        /// </param>
        /// <param name="fromType">
        /// </param>
        /// <param name="result">
        /// </param>
        /// <param name="custom">
        /// </param>
        public static void WriteBitcast(this LlvmWriter llvmWriter, OpCodePart opCode, IType fromType, LlvmResult result, string custom)
        {
            var writer = llvmWriter.Output;

            llvmWriter.WriteSetResultNumber(opCode, null);
            writer.Write("bitcast ");
            fromType.WriteTypePrefix(writer, true);
            writer.Write(' ');
            llvmWriter.WriteResultNumber(result);
            writer.Write(" to ");
            writer.Write(custom);

            writer.WriteLine(string.Empty);
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCodeMethodInfo">
        /// </param>
        /// <param name="methodBase">
        /// </param>
        /// <param name="isVirtual">
        /// </param>
        /// <param name="hasThis">
        /// </param>
        /// <param name="isCtor">
        /// </param>
        /// <param name="thisResultNumber">
        /// </param>
        /// <param name="tryClause">
        /// </param>
        public static void WriteCall(
            this LlvmWriter llvmWriter,
            OpCodePart opCodeMethodInfo,
            IMethod methodBase,
            bool isVirtual,
            bool hasThis,
            bool isCtor,
            LlvmResult thisResultNumber,
            TryClause tryClause)
        {
            if (opCodeMethodInfo.HasResult)
            {
                return;
            }

            var methodInfo = methodBase;

            if (methodInfo != null
                && methodInfo.ReturnType.IsStructureType()
                && !opCodeMethodInfo.UsedBy.Any(Code.Ldfld, Code.Ldflda, Code.Call, Code.Callvirt, Code.Box, Code.Unbox, Code.Unbox_Any)
                && opCodeMethodInfo.DestinationName == null)
            {
                // You should not allocate it yourself, as result of function should be stored in DestinationName or in Return Value such as "agg.return"
                // so if Destination is null means that call is not ready and will be ready later when DestinationName is set
                // note: if method which returns structure used by "Field" when we need to generate temp result of a function in stack
                return;
            }

            var writer = llvmWriter.Output;

            llvmWriter.CheckIfExternalDeclarationIsRequired(methodBase);

            var preProcessedOperandDirectResults = new List<bool>();

            if (opCodeMethodInfo.OpCodeOperands != null)
            {
                var index = 0;
                foreach (var operand in opCodeMethodInfo.OpCodeOperands)
                {
                    preProcessedOperandDirectResults.Add(llvmWriter.PreProcessOperand(writer, opCodeMethodInfo, index));
                    index++;
                }
            }

            var thisType = methodBase.DeclaringType;
            thisType.UseAsClass = true;

            var hasThisArgument = hasThis && opCodeMethodInfo.OpCodeOperands != null && opCodeMethodInfo.OpCodeOperands.Length > 0;
            var opCodeFirstOperand = opCodeMethodInfo.OpCodeOperands != null && opCodeMethodInfo.OpCodeOperands.Length > 0
                                         ? opCodeMethodInfo.OpCodeOperands[0]
                                         : null;
            var resultOfFirstOperand = opCodeFirstOperand != null ? llvmWriter.ResultOf(opCodeFirstOperand) : null;

            LlvmResult virtualMethodAddressResultNumber = null;
            var isIndirectMethodCall = isVirtual
                                       && (methodBase.IsAbstract || methodBase.IsVirtual
                                           || (thisType.IsInterface && thisType.TypeEquals(resultOfFirstOperand.IType)));

            var ownerOfExplicitInterface = isVirtual && thisType.IsInterface && thisType.TypeNotEquals(resultOfFirstOperand.IType)
                                               ? resultOfFirstOperand.IType
                                               : null;
            var requiredType = ownerOfExplicitInterface != null ? resultOfFirstOperand.IType : null;
            if (requiredType != null)
            {
                thisType = requiredType;
            }

            if (isIndirectMethodCall && !methodBase.DeclaringType.Equals(thisType) && methodBase.DeclaringType.IsInterface)
            {
                // this is explicit call of interface
                isIndirectMethodCall = false;
            }

            if (isIndirectMethodCall)
            {
                llvmWriter.GenerateVirtualCall(opCodeMethodInfo, methodInfo, thisType, opCodeFirstOperand, resultOfFirstOperand, ref virtualMethodAddressResultNumber, ref requiredType);
            }

            // check if you need to cast this parameter
            if (hasThisArgument)
            {
                if (thisType.IsClassCastRequired(opCodeFirstOperand))
                {
                    writer.WriteLine("; Cast of 'This' parameter");
                    llvmWriter.WriteCast(opCodeFirstOperand, opCodeFirstOperand.Result, thisType);
                    writer.WriteLine(string.Empty);
                }

                thisType.UseAsClass = false;
                var isPrimitive = thisType.IsPrimitiveType();
                thisType.UseAsClass = true;
                if (isPrimitive)
                {
                    writer.WriteLine("; Box Primitive type for 'This' parameter");
                    llvmWriter.WriteConvertValueTypeToReferenceType(writer, opCodeFirstOperand, thisType);
                }

            }

            // check if you need to cast parameter
            if (opCodeMethodInfo.OpCodeOperands != null)
            {
                llvmWriter.PreProcessCallParameters(opCodeMethodInfo, methodBase, preProcessedOperandDirectResults);
            }

            if (methodInfo != null && !methodInfo.ReturnType.IsVoid() && opCodeMethodInfo.DestinationName == null)
            {
                llvmWriter.WriteSetResultNumber(opCodeMethodInfo, methodInfo.ReturnType);
            }

            var returnFullyDefinedReference = opCodeMethodInfo.Result != null ? opCodeMethodInfo.Result.ToFullyDefinedReference() : null;

            // allocate space for structure if return type is structure
            if (methodInfo != null && methodInfo.ReturnType.IsStructureType())
            {
                if (opCodeMethodInfo.DestinationName == null)
                {
                    // we need to store temp result of struct in stack to be used by "Ldfld, Ldflda"
                    llvmWriter.WriteAlloca(methodInfo.ReturnType);
                    writer.WriteLine(string.Empty);
                }
                else
                {
                    returnFullyDefinedReference = new FullyDefinedReference(opCodeMethodInfo.DestinationName, methodInfo.ReturnType);
                }
            }

            if (tryClause != null)
            {
                writer.Write("invoke ");
            }
            else
            {
                writer.Write("call ");
            }

            if (methodInfo != null && !methodInfo.ReturnType.IsVoid() && !methodInfo.ReturnType.IsStructureType())
            {
                methodInfo.ReturnType.WriteTypePrefix(writer, false);

                llvmWriter.CheckIfExternalDeclarationIsRequired(methodInfo.ReturnType);
            }
            else
            {
                // this is constructor
                writer.Write("void");
            }

            writer.Write(' ');

            if (isIndirectMethodCall)
            {
                llvmWriter.WriteResultNumber(virtualMethodAddressResultNumber);
            }
            else
            {
                if (methodBase.IsInternalCall)
                {
                    writer.Write("(...)* ");
                }

                llvmWriter.WriteMethodDefinitionName(writer, methodBase, ownerOfExplicitInterface);
            }

            llvmWriter.ActualWrite(
                writer,
                opCodeMethodInfo.OpCodeOperands,
                methodBase.GetParameters(),
                isVirtual,
                hasThis,
                isCtor,
                preProcessedOperandDirectResults,
                thisResultNumber,
                thisType,
                returnFullyDefinedReference,
                methodInfo != null ? methodInfo.ReturnType : null);

            if (tryClause != null)
            {
                var nextOpCode = opCodeMethodInfo.NextOpCode(llvmWriter);
                var nextIsBrunch = nextOpCode.Any(Code.Br, Code.Br_S, Code.Leave, Code.Leave_S);
                var nextAddress = nextIsBrunch ? nextOpCode.JumpAddress() : opCodeMethodInfo.AddressEnd;

                var useBlockJumpRandomAddress = false;
                if (nextAddress == 0)
                {
                    useBlockJumpRandomAddress = true;
                    nextAddress = llvmWriter.GetBlockJumpAddress();
                }

                writer.WriteLine(string.Empty);
                writer.Indent++;
                writer.WriteLine("to label %.{0}{1} unwind label %.catch{2}", useBlockJumpRandomAddress ? "b" : "a", nextAddress, tryClause.Catches.First().Offset);
                writer.Indent--;
                if (!nextIsBrunch)
                {
                    writer.Indent--;
                    writer.WriteLine(".{0}{1}:", useBlockJumpRandomAddress ? "b" : "a", useBlockJumpRandomAddress ? nextAddress : opCodeMethodInfo.GroupAddressEnd);
                    writer.Indent++;
                    opCodeMethodInfo.NextOpCode(llvmWriter).JumpProcessed = true;
                }
            }
        }

        public static void GenerateVirtualCall(this LlvmWriter llvmWriter, OpCodePart opCodeMethodInfo, IMethod methodInfo, IType thisType, OpCodePart opCodeFirstOperand, BaseWriter.ReturnResult resultOfirstOperand, ref LlvmResult virtualMethodAddressResultNumber, ref IType requiredType)
        {
            var writer = llvmWriter.Output;

            if (thisType.IsInterface && !resultOfirstOperand.IType.Equals(thisType))
            {
                // we need to extract interface from an object
                requiredType = thisType;
            }

            // get pointer to Virtual Table and call method
            // 1) get pointer to virtual table
            writer.WriteLine("; Get Virtual Table");
            llvmWriter.UnaryOper(writer, opCodeMethodInfo, "bitcast", requiredType);
            writer.Write(" to ");
            llvmWriter.WriteMethodPointerType(writer, methodInfo);
            writer.WriteLine("**");
            var pointerToInterfaceVirtualTablePointersResultNumber = opCodeMethodInfo.Result;

            // load pointer
            llvmWriter.WriteSetResultNumber(opCodeMethodInfo, llvmWriter.ResolveType("System.Byte").CreatePointer().CreatePointer());
            writer.Write("load ");
            llvmWriter.WriteMethodPointerType(writer, methodInfo);
            writer.Write("** ");
            llvmWriter.WriteResultNumber(pointerToInterfaceVirtualTablePointersResultNumber);
            writer.WriteLine(string.Empty);
            var virtualTableOfMethodPointersResultNumber = opCodeMethodInfo.Result;

            // get address of a function
            llvmWriter.WriteSetResultNumber(opCodeMethodInfo, llvmWriter.ResolveType("System.Byte").CreatePointer());
            writer.Write("getelementptr inbounds ");
            llvmWriter.WriteMethodPointerType(writer, methodInfo);
            writer.Write("* ");
            llvmWriter.WriteResultNumber(virtualTableOfMethodPointersResultNumber);
            writer.WriteLine(", i64 {0}", (requiredType ?? thisType).GetVirtualMethodIndex(methodInfo));
            var pointerToFunctionPointerResultNumber = opCodeMethodInfo.Result;

            // load method address
            llvmWriter.WriteSetResultNumber(opCodeMethodInfo, llvmWriter.ResolveType("System.Byte").CreatePointer());
            writer.Write("load ");
            llvmWriter.WriteMethodPointerType(writer, methodInfo);
            writer.Write("* ");
            llvmWriter.WriteResultNumber(pointerToFunctionPointerResultNumber);
            writer.WriteLine(string.Empty);

            // remember virtual method address result
            virtualMethodAddressResultNumber = opCodeMethodInfo.Result;

            if (thisType.IsInterface)
            {
                opCodeFirstOperand.Result = virtualTableOfMethodPointersResultNumber;

                llvmWriter.WriteGetThisPointerFromInterfacePointer(
                    writer, opCodeMethodInfo, methodInfo, thisType, pointerToInterfaceVirtualTablePointersResultNumber);

                var thisPointerResultNumber = opCodeMethodInfo.Result;

                // set ot for Call op code
                opCodeMethodInfo.OpCodeOperands[0].Result = thisPointerResultNumber;
            }
        }

        public static void PreProcessCallParameters(this LlvmWriter llvmWriter, OpCodePart opCodeMethodInfo, IMethod methodBase, IList<bool> preProcessedOperandDirectResults)
        {
            var writer = llvmWriter.Output;

            var parameters = methodBase.GetParameters();
            var index = opCodeMethodInfo.OpCodeOperands.Count() - parameters.Count();
            foreach (var parameter in parameters)
            {
                var operand = opCodeMethodInfo.OpCodeOperands[index];

                if (parameter.ParameterType.IsClassCastRequired(operand))
                {
                    writer.WriteLine("; Cast of '{0}' parameter", parameter.Name);
                    llvmWriter.WriteCast(operand, operand.Result, parameter.ParameterType);
                }

                if (operand.HasResult && parameter.ParameterType.IsIntValueTypeCastRequired(operand.Result.Type))
                {
                    llvmWriter.AdjustIntConvertableTypes(writer, operand, preProcessedOperandDirectResults[index], parameter.ParameterType);
                }

                index++;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCode">
        /// </param>
        /// <param name="fromResult">
        /// </param>
        /// <param name="toType">
        /// </param>
        /// <param name="appendReference">
        /// </param>
        // TODO: fix WriteCast, you should not use explicit appending of reference
        public static bool WriteCast(this LlvmWriter llvmWriter, OpCodePart opCode, LlvmResult fromResult, IType toType, bool appendReference = false)
        {
            var writer = llvmWriter.Output;

            if (!fromResult.Type.IsInterface && toType.IsInterface)
            {
                if (fromResult.Type.HasInterface(toType))
                {
                    opCode.Result = fromResult;
                    llvmWriter.WriteInterfaceAccess(writer, opCode, fromResult.Type, toType);
                }
                else
                {
                    llvmWriter.WriteDynamicCast(writer, opCode, fromResult, toType);
                }
            }
            else
            {
                llvmWriter.WriteSetResultNumber(opCode, toType);
                writer.Write("bitcast ");
                fromResult.Type.WriteTypePrefix(writer, true);
                writer.Write(' ');
                llvmWriter.WriteResultNumber(fromResult);
                writer.Write(" to ");
                toType.WriteTypePrefix(writer, true);
                if (appendReference)
                {
                    // result should be array
                    writer.Write('*');
                }
            }

            writer.WriteLine(string.Empty);

            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCode">
        /// </param>
        /// <param name="fromType">
        /// </param>
        /// <param name="custromName">
        /// </param>
        /// <param name="toType">
        /// </param>
        /// <param name="appendReference">
        /// </param>
        /// <exception cref="NotImplementedException">
        /// </exception>
        public static void WriteCast(
            this LlvmWriter llvmWriter, OpCodePart opCode, IType fromType, string custromName, IType toType, bool appendReference = false, bool doNotConvert = false)
        {
            var writer = llvmWriter.Output;

            if (!fromType.IsInterface && toType.IsInterface)
            {
                throw new NotImplementedException();

                ////opCode.Result = res;
                ////this.WriteInterfaceAccess(writer, opCode, fromType, toType);
            }
            else
            {
                llvmWriter.WriteSetResultNumber(opCode, toType);
                writer.Write("bitcast ");
                fromType.WriteTypePrefix(writer, true);
                writer.Write(' ');
                writer.Write(custromName);
                writer.Write(" to ");
                toType.WriteTypePrefix(writer, true);
                if (appendReference)
                {
                    // result should be array
                    writer.Write('*');
                }
            }

            writer.WriteLine(string.Empty);
        }

        public static void WriteLlvmLoad(
            this LlvmWriter llvmWriter, OpCodePart opCode, IType typeToLoad, LlvmResult source, bool appendReference = true, bool structAsRef = false)
        {
            llvmWriter.WriteLlvmLoad(opCode, typeToLoad, new FullyDefinedReference(source.ToString(), source.Type), appendReference, structAsRef);
        }

        public static void WriteLlvmLoad(
            this LlvmWriter llvmWriter, OpCodePart opCode, FullyDefinedReference source, bool appendReference = true, bool structAsRef = false)
        {
            llvmWriter.WriteLlvmLoad(opCode, source.Type, source, appendReference, structAsRef);
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="opCode">
        /// </param>
        /// <param name="typeToLoad">
        /// </param>
        /// <param name="source">
        /// </param>
        /// <param name="appendReference">
        /// </param>
        /// <param name="structAsRef">
        /// </param>
        public static void WriteLlvmLoad(
            this LlvmWriter llvmWriter, OpCodePart opCode, IType typeToLoad, FullyDefinedReference source, bool appendReference = true, bool structAsRef = false)
        {
            var writer = llvmWriter.Output;

            Debug.Assert(!typeToLoad.IsStructureType() || typeToLoad.IsStructureType() && opCode.DestinationName != null);

            if (!typeToLoad.IsStructureType() || structAsRef || opCode.DestinationName == null)
            {
                if (opCode.HasResult)
                {
                    return;
                }

                ////Debug.Assert(source.Type.IsPointer);
                var dereferencedType = source.Type.IsPointer ? source.Type.GetElementType() : null;

                var effectiveSource = source;

                // check if you need bitcast pointer type
                if (!typeToLoad.IsPointer && dereferencedType != null && typeToLoad.TypeNotEquals(dereferencedType))
                {
                    // check if you need cast here
                    llvmWriter.WriteBitcast(opCode, source, typeToLoad);
                    writer.WriteLine(string.Empty);
                    effectiveSource = opCode.Result.ToFullyDefinedReference();
                }

                if (dereferencedType == null
                    && !source.Type.IsPointer
                    && !source.Type.IsByRef
                    && typeToLoad.IntTypeBitSize() != source.Type.IntTypeBitSize()
                    && typeToLoad.IntTypeBitSize() > 0)
                {
                    // check if you need cast here
                    llvmWriter.WriteIntToPtr(opCode, source, typeToLoad);
                    writer.WriteLine(string.Empty);
                    effectiveSource = opCode.Result.ToFullyDefinedReference();
                }

                llvmWriter.WriteSetResultNumber(opCode, typeToLoad);

                // last part
                writer.Write("load ");
                typeToLoad.WriteTypePrefix(writer, structAsRef);
                if (appendReference)
                {
                    // add reference to type
                    writer.Write('*');
                }

                writer.Write(' ');
                writer.Write(effectiveSource.ToString());

                // TODO: optional do we need to calculate it propertly?
                writer.Write(", align " + LlvmWriter.PointerSize);
            }
            else
            {
                llvmWriter.WriteCopyStruct(writer, opCode, typeToLoad, source.Name, opCode.DestinationName);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="index">
        /// </param>
        /// <param name="asReference">
        /// </param>
        public static void WriteLlvmLocalVarAccess(this LlvmWriter llvmWriter, int index, bool asReference = false)
        {
            var writer = llvmWriter.Output;

            var localType = llvmWriter.LocalInfo[index].LocalType;

            localType.WriteTypePrefix(writer, false);
            if (asReference)
            {
                writer.Write('*');
            }

            writer.Write(' ');
            writer.Write(llvmWriter.GetLocalVarName(index));

            // TODO: optional do we need to calculate it propertly?
            writer.Write(", align " + LlvmWriter.PointerSize);
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="type">
        /// </param>
        /// <param name="op1">
        /// </param>
        /// <param name="op2">
        /// </param>
        public static void WriteMemCopy(this LlvmWriter llvmWriter, IType type, LlvmResult op1, LlvmResult op2)
        {
            var writer = llvmWriter.Output;

            writer.WriteLine(
                "call void @llvm.memcpy.p0i8.p0i8.i32(i8* {0}, i8* {1}, i32 {2}, i32 {3}, i1 false)",
                op1,
                op2,
                type.GetTypeSize(),
                LlvmWriter.PointerSize /*Align*/);
        }

        /// <summary>
        /// </summary>
        /// <param name="llvmWriter">
        /// </param>
        /// <param name="type">
        /// </param>
        /// <param name="op1">
        /// </param>
        public static void WriteMemSet(this LlvmWriter llvmWriter, IType type, LlvmResult op1)
        {
            var writer = llvmWriter.Output;

            writer.Write(
                "call void @llvm.memset.p0i8.i32(i8* {0}, i8 0, i32 {1}, i32 {2}, i1 false)",
                op1,
                type.GetTypeSize(),
                LlvmWriter.PointerSize /*Align*/);
        }

        public static void LlvmConvert(this LlvmWriter llvmWriter, OpCodePart opCode, string realConvert, string intConvert, string toType, bool toAddress, params IType[] typesToExclude)
        {
            var writer = llvmWriter.Output;

            var resultOf = llvmWriter.ResultOf(opCode.OpCodeOperands[0]);
            var areBothPointers = ((resultOf.IType.IsPointer || resultOf.IType.IsByRef) && toAddress);
            if (!typesToExclude.Any(t => resultOf.IType.TypeEquals(t)) && !areBothPointers)
            {
                if (resultOf.IType.IsReal())
                {
                    llvmWriter.UnaryOper(writer, opCode, realConvert, options: LlvmWriter.OperandOptions.GenerateResult);
                }
                else if (resultOf.IType.IsPointer || resultOf.IType.IsByRef)
                {
                    llvmWriter.UnaryOper(writer, opCode, "ptrtoint", options: LlvmWriter.OperandOptions.GenerateResult);
                }
                else if (toType.EndsWith("*") || toType.EndsWith("&"))
                {
                    llvmWriter.UnaryOper(writer, opCode, "inttoptr", options: LlvmWriter.OperandOptions.GenerateResult);
                }
                else
                {
                    llvmWriter.UnaryOper(writer, opCode, intConvert, options: LlvmWriter.OperandOptions.GenerateResult);
                }

                writer.Write(" to {0}", toType);
            }
            else
            {
                var isDirectValue = llvmWriter.IsDirectValue(opCode.OpCodeOperands[0]);
                if (!isDirectValue)
                {
                    if (opCode.OpCodeOperands[0].Result != null)
                    {
                        opCode.Result = opCode.OpCodeOperands[0].Result;
                    }
                    else
                    {
                        llvmWriter.ActualWrite(writer, opCode.OpCodeOperands[0]);
                    }
                }
                else
                {
                    llvmWriter.ActualWrite(writer, opCode.OpCodeOperands[0]);
                }
            }
        }

        public static void LlvmIntConvert(this LlvmWriter llvmWriter, OpCodePart opCode, string intConvert, string toType)
        {
            var writer = llvmWriter.Output;

            var incomingResult = opCode.Result;

            var directResult1 = llvmWriter.PreProcess(writer, opCode);
            llvmWriter.ProcessOperator(writer, opCode, intConvert, opCode.Result.Type);

            var returnResult = opCode.Result;

            opCode.Result = incomingResult;

            llvmWriter.PostProcess(writer, opCode, directResult1);

            writer.Write(" to {0}", toType);

            opCode.Result = returnResult;
        }
    }
}