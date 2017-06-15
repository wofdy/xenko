// Copyright (c) 2011-2017 Silicon Studio Corp. All rights reserved. (https://www.siliconstudio.co.jp)
// See LICENSE.md for full license information.
using System.Collections.Generic;
using System.Linq;
using SiliconStudio.Assets.Visitors;
using SiliconStudio.Core.IO;
using SiliconStudio.Core.Reflection;

namespace SiliconStudio.Assets.Tracking
{
    public class SourceFilesCollector : AssetVisitorBase
    {
        private Dictionary<UFile, bool> sourceFiles;
        private HashSet<UFile> compilationInputFiles;
        private Dictionary<MemberPath, UFile> sourceMembers;

        public Dictionary<UFile, bool> GetSourceFiles(Asset asset)
        {
            sourceFiles = new Dictionary<UFile, bool>();
            Visit(asset);
            var result = sourceFiles;
            sourceFiles = null;
            return result;
        }

        public HashSet<UFile> GetCompilationInputFiles(Asset asset)
        {
            compilationInputFiles = new HashSet<UFile>();
            Visit(asset);
            var result = compilationInputFiles;
            compilationInputFiles = null;
            return result;
        }

        public Dictionary<MemberPath, UFile> GetSourceMembers(Asset asset)
        {
            sourceMembers = new Dictionary<MemberPath, UFile>();
            Visit(asset);
            var result = sourceMembers;
            sourceMembers = null;
            return result;
        }

        public override void VisitObjectMember(object container, ObjectDescriptor containerDescriptor, IMemberDescriptor member, object value)
        {
            if (sourceFiles != null)
            {
                if (member.Type == typeof(UFile) && value != null)
                {
                    var file = (UFile)value;
                    if (!string.IsNullOrWhiteSpace(file.ToString()))
                    {
                        var attribute = member.GetCustomAttributes<SourceFileMemberAttribute>(true).SingleOrDefault();
                        if (attribute != null)
                        {
                            if (!sourceFiles.ContainsKey(file))
                            {
                                sourceFiles.Add(file, attribute.UpdateAssetIfChanged);
                            }
                            else if (attribute.UpdateAssetIfChanged)
                            {
                                // If the file has already been collected, just update whether it should update the asset when changed
                                sourceFiles[file] = true;
                            }
                        }
                    }
                }
            }
            if (compilationInputFiles != null)
            {
                if (member.Type == typeof(UFile) && value != null)
                {
                    var file = (UFile)value;
                    if (!string.IsNullOrWhiteSpace(file.ToString()))
                    {
                        var attribute = member.GetCustomAttributes<SourceFileMemberAttribute>(true).SingleOrDefault();
                        if (attribute != null && !attribute.Optional)
                        {
                            compilationInputFiles.Add(file);
                        }
                    }
                }
            }
            if (sourceMembers != null)
            {
                if (member.Type == typeof(UFile))
                {
                    var attribute = member.GetCustomAttributes<SourceFileMemberAttribute>(true).SingleOrDefault();
                    if (attribute != null)
                    {
                        sourceMembers[CurrentPath.Clone()] = value as UFile;
                    }
                }
            }
            base.VisitObjectMember(container, containerDescriptor, member, value);
        }
    }
}
