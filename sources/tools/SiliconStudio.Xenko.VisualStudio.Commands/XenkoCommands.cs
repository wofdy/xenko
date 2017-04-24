// Copyright (c) 2014-2017 Silicon Studio Corp. All rights reserved. (https://www.siliconstudio.co.jp)
// See LICENSE.md for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using NShader;
using SiliconStudio.Assets;
using SiliconStudio.Xenko.Shaders.Parser;
using SiliconStudio.Xenko.VisualStudio.BuildEngine;
using SiliconStudio.Xenko.VisualStudio.Commands.Shaders;
using SiliconStudio.Shaders.Ast;
using SiliconStudio.Shaders.Utility;

namespace SiliconStudio.Xenko.VisualStudio.Commands
{
    public class XenkoCommands : IXenkoCommands
    {
        public void Initialize(string xenkoSdkDir)
        {
            DirectoryHelper.PackageDirectoryOverride = xenkoSdkDir;
            // Don't necessarely initialize the shaders
            //XenkoShaderParser.Initialize();
        }

        public bool ShouldReload()
        {
            // This is implemented in the proxy only
            throw new NotImplementedException();
        }

        public void StartRemoteBuildLogServer(IBuildMonitorCallback buildMonitorCallback, string logPipeUrl)
        {
            new PackageBuildMonitorRemote(buildMonitorCallback, logPipeUrl);
        }

        public byte[] GenerateShaderKeys(string inputFileName, string inputFileContent)
        {
            return ShaderKeyFileHelper.GenerateCode(inputFileName, inputFileContent);
        }

        public RawShaderNavigationResult AnalyzeAndGoToDefinition(string sourceCode, RawSourceSpan span)
        {
            var rawResult = new RawShaderNavigationResult();

            var navigation = new ShaderNavigation();

            var shaderDirectories = CollectShadersDirectories(null);

            if (span.File != null)
            {
                var dirName = Path.GetDirectoryName(span.File);
                if (dirName != null)
                {
                    shaderDirectories.Add(dirName);
                }
            }

            var resultAnalysis = navigation.AnalyzeAndGoToDefinition(sourceCode, new SiliconStudio.Shaders.Ast.SourceLocation(span.File, 0, span.Line, span.Column), shaderDirectories);

            if (resultAnalysis.DefinitionLocation.Location.FileSource != null)
            {
                rawResult.DefinitionSpan = ConvertToRawLocation(resultAnalysis.DefinitionLocation);
            }

            foreach (var message in resultAnalysis.Messages.Messages)
            {
                rawResult.Messages.Add(ConvertToRawMessage(message));
            }

            return rawResult;
        }

        private static RawSourceSpan ConvertToRawLocation(SourceSpan span)
        {
            return new RawSourceSpan()
            {
                File = span.Location.FileSource,
                Line = span.Location.Line,
                EndLine = span.Location.Line,
                Column = span.Location.Column,
                EndColumn = span.Location.Column + span.Length
            };
        }

        private static RawShaderAnalysisMessage ConvertToRawMessage(ReportMessage message)
        {
            return new RawShaderAnalysisMessage()
            {
                Span = ConvertToRawLocation(message.Span),
                Text = message.Text,
                Code = message.Code,
                Type = ConvertToStringLevel(message.Level)
            };
        }

        private static string ConvertToStringLevel(ReportMessageLevel level)
        {
            return level.ToString().ToLowerInvariant();
        }

        private List<string> CollectShadersDirectories(string packagePath)
        {
            if (packagePath == null)
            {
                packagePath = PackageStore.Instance.DefaultPackage.FullPath;
            }

            var defaultLoad = PackageLoadParameters.Default();
            defaultLoad.AutoCompileProjects = false;
            defaultLoad.AutoLoadTemporaryAssets = false;
            defaultLoad.ConvertUPathToAbsolute = false;
            defaultLoad.GenerateNewAssetIds = false;
            defaultLoad.LoadAssemblyReferences = false;

            var sessionResult = PackageSession.Load(packagePath, defaultLoad);

            if (sessionResult.HasErrors)
            {
                // TODO: Throw an error
                return null;
            }

            var session = sessionResult.Session;

            var assetsPaths = new List<string>();
            foreach (var package in session.Packages)
            {
                foreach (var profile in package.Profiles)
                {
                    foreach (var folder in profile.AssetFolders)
                    {
                        var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packagePath), folder.Path));

                        assetsPaths.Add(fullPath);
                        assetsPaths.AddRange(Directory.EnumerateDirectories(fullPath, "*.*", SearchOption.AllDirectories));
                    }
                }
            }
            return assetsPaths;
        }
    }
}