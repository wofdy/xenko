// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;

using SiliconStudio.Core.Mathematics;
using SiliconStudio.Paradox.Engine.Graphics;
using SiliconStudio.Paradox.Graphics;

namespace SiliconStudio.Paradox.Effects.Shadows
{
    /// <summary>
    /// An atlas of shadow maps.
    /// </summary>
    public class ShadowMapAtlasTexture : GuillotinePacker
    {
        public ShadowMapAtlasTexture(Texture texture)
        {
            if (texture == null) throw new ArgumentNullException("texture");
            Texture = texture;
            Clear(Texture.Width, Texture.Height);
            Width = texture.Width;
            Height = texture.Height;

            RenderFrame = RenderFrame.FromTexture((Texture)null, texture);
        }

        public readonly int Width;

        public readonly int Height;

        public Type FilterType;

        public readonly Texture Texture;

        public readonly RenderFrame RenderFrame;

        private bool IsRenderTargetCleared;

        public override void Clear()
        {
            base.Clear();
            IsRenderTargetCleared = false;
        }

        public void ClearRenderTarget(RenderContext context)
        {
            if (!IsRenderTargetCleared)
            {
                context.GraphicsDevice.Clear(Texture, DepthStencilClearOptions.DepthBuffer);
                IsRenderTargetCleared = true;
            }
        }
    }
}