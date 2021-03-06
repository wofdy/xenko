// Copyright (c) 2014-2017 Silicon Studio Corp. All rights reserved. (https://www.siliconstudio.co.jp)
// See LICENSE.md for full license information.

using SharpDX.DirectWrite;
using SiliconStudio.Assets.Compiler;
using SiliconStudio.Core;

namespace SiliconStudio.Xenko.Assets.SpriteFont
{
    [DataContract("FontProviderBase")]
    public abstract class FontProviderBase
    {
        [DataMemberIgnore]
        public virtual Graphics.Font.FontStyle Style { get; set; } = Graphics.Font.FontStyle.Regular;

        /// <summary>
        /// Gets the associated <see cref="FontFace"/>
        /// </summary>
        /// <returns><see cref="FontFace"/> from the specified source or <c>null</c> if not found</returns>
        public abstract FontFace GetFontFace();

        /// <summary>
        /// Gets the actual file path to the font file
        /// </summary>
        /// <returns>Path to the font file</returns>
        public abstract string GetFontPath(AssetCompilerResult result = null);

        /// <summary>
        /// Gets the actual font name
        /// </summary>
        /// <returns>The name of the font</returns>
        public abstract string GetFontName();
    }
}
