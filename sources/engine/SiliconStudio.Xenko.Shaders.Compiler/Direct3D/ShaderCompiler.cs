﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
#if SILICONSTUDIO_PLATFORM_WINDOWS // Need SharpDX
using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX;
using SharpDX.D3DCompiler;
using SiliconStudio.Core.Diagnostics;
using SiliconStudio.Core.Storage;
using SiliconStudio.Xenko.Rendering;
using SiliconStudio.Xenko.Graphics;
using ConstantBufferType = SiliconStudio.Xenko.Shaders.ConstantBufferType;
using ShaderBytecode = SiliconStudio.Xenko.Shaders.ShaderBytecode;
using ShaderVariableType = SharpDX.D3DCompiler.ShaderVariableType;

namespace SiliconStudio.Xenko.Shaders.Compiler.Direct3D
{
    internal class ShaderCompiler : IShaderCompiler
    {
        public ShaderBytecodeResult Compile(string shaderSource, string entryPoint, ShaderStage stage, CompilerParameters compilerParameters, EffectReflection reflection, string sourceFilename = null)
        {
            var isDebug = compilerParameters.EffectParameters.Debug;
            var optimLevel = compilerParameters.EffectParameters.OptimizationLevel;
            var profile = compilerParameters.EffectParameters.Profile;
            
            var shaderModel = ShaderStageToString(stage) + "_" + ShaderProfileFromGraphicsProfile(profile);

            var shaderFlags = ShaderFlags.None;
            if (isDebug)
            {
                shaderFlags = ShaderFlags.Debug;
            }
            switch (optimLevel)
            {
                case 0:
                    shaderFlags |= ShaderFlags.OptimizationLevel0;
                    break;
                case 1:
                    shaderFlags |= ShaderFlags.OptimizationLevel1;
                    break;
                case 2:
                    shaderFlags |= ShaderFlags.OptimizationLevel2;
                    break;
                case 3:
                    shaderFlags |= ShaderFlags.OptimizationLevel3;
                    break;
            }
            SharpDX.Configuration.ThrowOnShaderCompileError = false;

            // Compile using D3DCompiler
            var compilationResult = SharpDX.D3DCompiler.ShaderBytecode.Compile(shaderSource, entryPoint, shaderModel, shaderFlags, EffectFlags.None, null, null, sourceFilename);

            var byteCodeResult = new ShaderBytecodeResult();

            if (compilationResult.HasErrors || compilationResult.Bytecode == null)
            {
                // Log compilation errors
                byteCodeResult.Error(compilationResult.Message);
            }
            else
            {
                // TODO: Make this optional
                try
                {
                    byteCodeResult.DisassembleText = compilationResult.Bytecode.Disassemble();
                }
                catch (SharpDXException)
                {
                }

                // As effect bytecode binary can changed when having debug infos (with d3dcompiler_47), we are calculating a bytecodeId on the stripped version
                var rawData = compilationResult.Bytecode.Strip(StripFlags.CompilerStripDebugInformation | StripFlags.CompilerStripReflectionData);
                var bytecodeId = ObjectId.FromBytes(rawData);
                byteCodeResult.Bytecode = new ShaderBytecode(bytecodeId, compilationResult.Bytecode.Data) { Stage = stage };

                // If compilation succeed, then we can update reflection.
                UpdateReflection(byteCodeResult.Bytecode, reflection, byteCodeResult);

                if (!string.IsNullOrEmpty(compilationResult.Message))
                {
                    byteCodeResult.Warning(compilationResult.Message);
                }
            }

            return byteCodeResult;
        }

        private void UpdateReflection(ShaderBytecode shaderBytecode, EffectReflection effectReflection, LoggerResult log)
        {
            var shaderReflectionRaw = new SharpDX.D3DCompiler.ShaderReflection(shaderBytecode);
            var shaderReflectionRawDesc = shaderReflectionRaw.Description;

            foreach (var constantBuffer in effectReflection.ConstantBuffers)
            {
                UpdateConstantBufferReflection(constantBuffer);
            }

            // Constant Buffers
            for (int i = 0; i < shaderReflectionRawDesc.ConstantBuffers; ++i)
            {
                var constantBufferRaw = shaderReflectionRaw.GetConstantBuffer(i);
                var constantBufferRawDesc = constantBufferRaw.Description;
                var linkBuffer = effectReflection.ConstantBuffers.First(buffer => buffer.Name == constantBufferRawDesc.Name);

                // TODO: Flags/bitfield?
                linkBuffer.Stage = shaderBytecode.Stage;

                ValidateConstantBufferReflection(constantBufferRaw, ref constantBufferRawDesc, linkBuffer, log);
            }

            // BoundResources
            for (int i = 0; i < shaderReflectionRawDesc.BoundResources; ++i)
            {
                var boundResourceDesc = shaderReflectionRaw.GetResourceBindingDescription(i);

                string linkKeyName = null;
                string resourceGroup = null;
                foreach (var linkResource in effectReflection.ResourceBindings)
                {
                    if (linkResource.Param.RawName == boundResourceDesc.Name && linkResource.Stage == ShaderStage.None)
                    {
                        linkKeyName = linkResource.Param.KeyName;
                        resourceGroup = linkResource.Param.ResourceGroup;
                        break;
                    }

                }

                if (linkKeyName == null)
                {
                    log.Error("Resource [{0}] has no link", boundResourceDesc.Name);
                }
                else
                {

                    var binding = GetResourceBinding(boundResourceDesc, linkKeyName, log);
                    binding.Stage = shaderBytecode.Stage;
                    binding.Param.ResourceGroup = resourceGroup;

                    effectReflection.ResourceBindings.Add(binding);
                }
            }
        }

        private EffectParameterResourceData GetResourceBinding(SharpDX.D3DCompiler.InputBindingDescription bindingDescriptionRaw, string name, LoggerResult log)
        {
            var paramClass = EffectParameterClass.Object;
            var paramType = EffectParameterType.Void;

            switch (bindingDescriptionRaw.Type)
            {
                case SharpDX.D3DCompiler.ShaderInputType.TextureBuffer:
                    paramType = EffectParameterType.TextureBuffer;
                    paramClass = EffectParameterClass.TextureBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.ConstantBuffer:
                    paramType = EffectParameterType.ConstantBuffer;
                    paramClass = EffectParameterClass.ConstantBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.Texture:
                    paramClass = EffectParameterClass.ShaderResourceView;
                    switch (bindingDescriptionRaw.Dimension)
                    {
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Buffer:
                            paramType = EffectParameterType.Buffer;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture1D:
                            paramType = EffectParameterType.Texture1D;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture1DArray:
                            paramType = EffectParameterType.Texture1DArray;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D:
                            paramType = EffectParameterType.Texture2D;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture2DArray:
                            paramType = EffectParameterType.Texture2DArray;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture2DMultisampled:
                            paramType = EffectParameterType.Texture2DMultisampled;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture2DMultisampledArray:
                            paramType = EffectParameterType.Texture2DMultisampledArray;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture3D:
                            paramType = EffectParameterType.Texture3D;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.TextureCube:
                            paramType = EffectParameterType.TextureCube;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.TextureCubeArray:
                            paramType = EffectParameterType.TextureCubeArray;
                            break;
                    }
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.Structured:
                    paramClass = EffectParameterClass.ShaderResourceView;
                    paramType = EffectParameterType.StructuredBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.ByteAddress:
                    paramClass = EffectParameterClass.ShaderResourceView;
                    paramType = EffectParameterType.ByteAddressBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewRWTyped:
                    paramClass = EffectParameterClass.UnorderedAccessView;
                    switch (bindingDescriptionRaw.Dimension)
                    {
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Buffer:
                            paramType = EffectParameterType.RWBuffer;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture1D:
                            paramType = EffectParameterType.RWTexture1D;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture1DArray:
                            paramType = EffectParameterType.RWTexture1DArray;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D:
                            paramType = EffectParameterType.RWTexture2D;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture2DArray:
                            paramType = EffectParameterType.RWTexture2DArray;
                            break;
                        case SharpDX.Direct3D.ShaderResourceViewDimension.Texture3D:
                            paramType = EffectParameterType.RWTexture3D;
                            break;
                    }
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewRWStructured:
                    paramClass = EffectParameterClass.UnorderedAccessView;
                    paramType = EffectParameterType.RWStructuredBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewRWByteAddress:
                    paramClass = EffectParameterClass.UnorderedAccessView;
                    paramType = EffectParameterType.RWByteAddressBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewAppendStructured:
                    paramClass = EffectParameterClass.UnorderedAccessView;
                    paramType = EffectParameterType.AppendStructuredBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewConsumeStructured:
                    paramClass = EffectParameterClass.UnorderedAccessView;
                    paramType = EffectParameterType.ConsumeStructuredBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.UnorderedAccessViewRWStructuredWithCounter:
                    paramClass = EffectParameterClass.UnorderedAccessView;
                    paramType = EffectParameterType.RWStructuredBuffer;
                    break;
                case SharpDX.D3DCompiler.ShaderInputType.Sampler:
                    paramClass = EffectParameterClass.Sampler;
                    paramType = EffectParameterType.Sampler;
                    break;
            }

            var binding = new EffectParameterResourceData()
                {
                    Param =
                        {
                            KeyName = name,
                            RawName = bindingDescriptionRaw.Name,
                            Class = paramClass,
                            Type = paramType
                        },
                    SlotStart = bindingDescriptionRaw.BindPoint,
                    SlotCount = bindingDescriptionRaw.BindCount,
                };

            return binding;
        }

        private void UpdateConstantBufferReflection(ShaderConstantBufferDescription reflectionConstantBuffer)
        {
            // Used to compute constant buffer size and member offsets (std140 rule)
            int constantBufferOffset = 0;

            // Fill members
            for (int index = 0; index < reflectionConstantBuffer.Members.Length; index++)
            {
                var member = reflectionConstantBuffer.Members[index];

                // Properly compute size and offset according to DX rules
                var memberSize = ComputeMemberSize(ref member, ref constantBufferOffset);

                // Align offset and store it as member offset
                member.Offset = constantBufferOffset;
                member.Size = memberSize;

                // Adjust offset for next item
                constantBufferOffset += memberSize;

                reflectionConstantBuffer.Members[index] = member;
            }

            // Round buffer size to next multiple of 16
            reflectionConstantBuffer.Size = (constantBufferOffset + 15) / 16 * 16;
        }

        private void ValidateConstantBufferReflection(ConstantBuffer constantBufferRaw, ref ConstantBufferDescription constantBufferRawDesc, ShaderConstantBufferDescription constantBuffer, LoggerResult log)
        {
            switch (constantBufferRawDesc.Type)
            {
                case SharpDX.D3DCompiler.ConstantBufferType.ConstantBuffer:
                    if (constantBuffer.Type != ConstantBufferType.ConstantBuffer)
                        log.Error($"Invalid buffer type for {constantBuffer.Name}: {constantBuffer.Type} instead of {ConstantBufferType.ConstantBuffer}");
                    break;
                case SharpDX.D3DCompiler.ConstantBufferType.TextureBuffer:
                    if (constantBuffer.Type != ConstantBufferType.TextureBuffer)
                        log.Error($"Invalid buffer type for {constantBuffer.Name}: {constantBuffer.Type} instead of {ConstantBufferType.TextureBuffer}");
                    break;
                default:
                    if (constantBuffer.Type != ConstantBufferType.Unknown)
                        log.Error($"Invalid buffer type for {constantBuffer.Name}: {constantBuffer.Type} instead of {ConstantBufferType.Unknown}");
                    break;
            }

            // ConstantBuffers variables
            for (int i = 0; i < constantBufferRawDesc.VariableCount; i++)
            {
                var variable = constantBufferRaw.GetVariable(i);
                var variableType = variable.GetVariableType();
                var variableDescription = variable.Description;
                var variableTypeDescription = variableType.Description;

                if (variableTypeDescription.Offset != 0)
                {
                    log.Error("Unexpected offset [{0}] for variable [{1}] in constant buffer [{2}]", variableTypeDescription.Offset, variableDescription.Name, constantBuffer.Name);
                }

                var binding = constantBuffer.Members[i];
                // Retrieve Link Member
                if (binding.Param.RawName != variableDescription.Name)
                {
                    log.Error("Variable [{0}] in constant buffer [{1}] has no link", variableDescription.Name, constantBuffer.Name);
                }
                else
                {
                    var parameter = new EffectParameterValueData()
                    {
                        Param =
                    {
                        Class = (EffectParameterClass)variableTypeDescription.Class,
                        Type = ConvertVariableValueType(variableTypeDescription.Type, log),
                        RawName = variableDescription.Name,
                    },
                        Offset = variableDescription.StartOffset,
                        Size = variableDescription.Size,
                        Count = variableTypeDescription.ElementCount == 0 ? 1 : variableTypeDescription.ElementCount,
                        RowCount = (byte)variableTypeDescription.RowCount,
                        ColumnCount = (byte)variableTypeDescription.ColumnCount,
                    };

                    if (parameter.Offset != binding.Offset
                        || parameter.Size != binding.Size
                        || parameter.Count != binding.Count
                        || parameter.RowCount != binding.RowCount
                        || parameter.ColumnCount != binding.ColumnCount)
                    {
                        log.Error("Variable [{0}] in constant buffer [{1}] binding doesn't match what was expected", variableDescription.Name, constantBuffer.Name);
                    }
                }
            }
            if (constantBuffer.Size != constantBufferRawDesc.Size)
            {
                log.Error($"Error precomputing buffer size for {constantBuffer.Name}: {constantBuffer.Size} instead of {constantBufferRawDesc.Size}");
            }
        }

        private static int ComputeMemberSize(ref EffectParameterValueData member, ref int constantBufferOffset)
        {
            var elementSize = ComputeTypeSize(member.Param.Type);
            int size;
            int alignment = 4;

            switch (member.Param.Class)
            {
                case EffectParameterClass.Scalar:
                    {
                        size = elementSize;
                        break;
                    }
                case EffectParameterClass.Color:
                case EffectParameterClass.Vector:
                    {
                        size = elementSize * member.ColumnCount;
                        break;
                    }
                case EffectParameterClass.MatrixColumns:
                    {
                        size = elementSize * (4 * (member.ColumnCount - 1) + member.RowCount);
                        break;
                    }
                case EffectParameterClass.MatrixRows:
                    {
                        size = elementSize * (4 * (member.RowCount - 1) + member.ColumnCount);
                        break;
                    }
                default:
                    throw new NotImplementedException();
            }

            // Array
            if (member.Count > 1)
            {
                var roundedSize = (size + 15) / 16 * 16; // Round up to vec4
                size += roundedSize * (member.Count - 1);
                alignment = 16;
            }

            // Align to float4 if it is bigger than leftover space in current float4
            if (constantBufferOffset / 16 != (constantBufferOffset + size - 1) / 16)
                alignment = 16;

            // Align offset and store it as member offset
            constantBufferOffset = (constantBufferOffset + alignment - 1) / alignment * alignment;

            return size;
        }

        private static int ComputeTypeSize(EffectParameterType type)
        {
            switch (type)
            {
                case EffectParameterType.Bool:
                case EffectParameterType.Float:
                case EffectParameterType.Int:
                case EffectParameterType.UInt:
                    return 4;
                case EffectParameterType.Double:
                    return 8;
                default:
                    throw new NotImplementedException();
            }
        }

        private static string ShaderStageToString(ShaderStage stage)
        {
            string shaderStageText;
            switch (stage)
            {
                case ShaderStage.Compute:
                    shaderStageText = "cs";
                    break;
                case ShaderStage.Vertex:
                    shaderStageText = "vs";
                    break;
                case ShaderStage.Hull:
                    shaderStageText = "hs";
                    break;
                case ShaderStage.Domain:
                    shaderStageText = "ds";
                    break;
                case ShaderStage.Geometry:
                    shaderStageText = "gs";
                    break;
                case ShaderStage.Pixel:
                    shaderStageText = "ps";
                    break;
                default:
                    throw new ArgumentException("Stage not supported", "stage");
            }
            return shaderStageText;
        }

        private static string ShaderProfileFromGraphicsProfile(GraphicsProfile graphicsProfile)
        {
            switch (graphicsProfile)
            {
                case GraphicsProfile.Level_9_1:
                    return "4_0_level_9_1";
                case GraphicsProfile.Level_9_2:
                    return "4_0_level_9_2";
                case GraphicsProfile.Level_9_3:
                    return "4_0_level_9_3";
                case GraphicsProfile.Level_10_0:
                    return "4_0";
                case GraphicsProfile.Level_10_1:
                    return "4_1";
                case GraphicsProfile.Level_11_0:
                case GraphicsProfile.Level_11_1:
                    return "5_0";
            }
            throw new ArgumentException("graphicsProfile");
        }
        private static readonly Dictionary<ShaderVariableType, EffectParameterType> MapTypes = new Dictionary<ShaderVariableType,EffectParameterType>()
            {
                {ShaderVariableType.Void                                 , EffectParameterType.Void                          },
                {ShaderVariableType.Bool                                 , EffectParameterType.Bool                          },
                {ShaderVariableType.Int                                  , EffectParameterType.Int                           },
                {ShaderVariableType.Float                                , EffectParameterType.Float                         },
                {ShaderVariableType.UInt                                 , EffectParameterType.UInt                          },
                {ShaderVariableType.UInt8                                , EffectParameterType.UInt8                         },
                {ShaderVariableType.Double                               , EffectParameterType.Double                        },
            };

        private EffectParameterType ConvertVariableValueType(ShaderVariableType type, LoggerResult log)
        {
            EffectParameterType effectParameterType;
            if (!MapTypes.TryGetValue(type, out effectParameterType))
            {
                log.Error("Type [{0}] from D3DCompiler not supported", type);
            }
            return effectParameterType;
        }
    }
}
#endif