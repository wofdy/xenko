﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.Collections.Generic;

using SiliconStudio.Core;
using SiliconStudio.Core.Mathematics;
using SiliconStudio.Paradox.Graphics;

namespace SiliconStudio.Paradox.Effects.Images
{
    /// <summary>
    /// Luminance effect.
    /// </summary>
    public class LuminanceEffect : ImageEffectBase
    {
        private readonly ImageEffect luminanceLogEffect;
        private readonly RenderTarget luminance1x1;
        private readonly GaussianBlur blur;
        private readonly Texture2D[] luminancesStaging;
        private readonly bool[] luminanceStagingUsed;
        private int currentLuminanceIndex;
        private readonly Half[] mapValue = new Half[1];
        private readonly List<RenderTarget> tempMipmaps;

        /// <summary>
        /// Initializes a new instance of the <see cref="LuminanceEffect"/> class.
        /// </summary>
        /// <param name="context">The context.</param>
        public LuminanceEffect(ImageEffectContext context) : base(context)
        {
            luminanceLogEffect = new ImageEffect(Context, "LuminanceLogEffect");

            luminance1x1 = Texture2D.New(GraphicsDevice, 1, 1, 1, PixelFormat.R16_Float, TextureFlags.ShaderResource | TextureFlags.RenderTarget).DisposeBy(this).ToRenderTarget().DisposeBy(this);

            // Create 1x1 luminances tempMipmaps to receive the average luminance
            luminancesStaging = new Texture2D[16];
            luminanceStagingUsed = new bool[luminancesStaging.Length];
            for (int i = 0; i < luminancesStaging.Length; i++)
            {
                luminancesStaging[i] = (Texture2D)luminance1x1.Texture.ToStaging();
                luminanceStagingUsed[i] = false;
            }

            tempMipmaps =  new List<RenderTarget>();

            blur = new GaussianBlur(context).DisposeBy(this);
            blur.Radius = 4;
        }

        public int UpscaleCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable calculation of <see cref="AverageLuminance"/> (default is false).
        /// </summary>
        /// <value><c>true</c> if to enable calculation of <see cref="AverageLuminance"/>; otherwise, <c>false</c>.</value>
        public bool EnableAverageLuminanceReadback { get; set; }

        /// <summary>
        /// Gets the average luminance calculated on the GPU. See remarks.
        /// </summary>
        /// <value>The average luminance.</value>
        /// <remarks>
        /// The average luminance is calculated on the GPU and readback with a few frames of delay, depending on the number of 
        /// frames in advance between command scheduling and actual execution on GPU.
        /// </remarks>
        public float AverageLuminance { get; private set; }

        /// <summary>
        /// Gets the average luminance 1x1 texture available after drawing this effect.
        /// </summary>
        /// <value>The average luminance texture.</value>
        public Texture AverageLuminanceTexture
        {
            get
            {
                return luminance1x1;
            }
        }

        protected override void DrawCore()
        {
            var inputTexture = GetSafeInput(0);
            var outputRenderTexture = GetOutput(0);

            // If no output, we are only calculating the average luminance.
            var lastSize = new Size3(1, 1, 1);
            if (outputRenderTexture != null)
            {
                lastSize = outputRenderTexture.Size;
                for (int i = 0; i < UpscaleCount; i++)
                    lastSize = Texture.NextMip(lastSize);
            }

            tempMipmaps.Clear();
            var luminanceMap = NewScopedRenderTarget2D(inputTexture.Width, inputTexture.Height, PixelFormat.R16_Float, 1);
            tempMipmaps.Add(luminanceMap);

            // Calculate the first luminance map
            luminanceLogEffect.SetInput(inputTexture);
            luminanceLogEffect.SetOutput(luminanceMap);
            luminanceLogEffect.Draw();

            var mipCount = Texture.CalculateMipLevels(inputTexture.Width, inputTexture.Height, 0) - 1;

            var nextSize = inputTexture.Size;
            int upscaleBaseIndex = 0;
            for (int i = 0; i < mipCount; i++)
            {
                nextSize = Texture.NextMip(nextSize);
                RenderTarget mipmap;

                if (i == (mipCount - 1))
                {
                    mipmap = luminance1x1;
                }
                else
                {
                    if (upscaleBaseIndex <= 1 && nextSize.Width <= lastSize.Width && nextSize.Height <= lastSize.Height)
                    {
                        upscaleBaseIndex = i + 1;
                    }

                    var renderTarget2D = NewScopedRenderTarget2D(nextSize.Width, nextSize.Height, PixelFormat.R16_Float, 1);
                    tempMipmaps.Add(renderTarget2D);
                    mipmap = renderTarget2D;
                }

                // Downscale/2
                Scaler.SetInput(tempMipmaps[i]);
                Scaler.SetOutput(mipmap);
                Scaler.Draw("Down/2");
            }

            // Calculate average luminance only if needed
            if (EnableAverageLuminanceReadback)
            {
                // TODO: This code could be moved to a general readback Effect utility.

                // Copy to staging resource
                GraphicsDevice.Copy(luminance1x1, luminancesStaging[currentLuminanceIndex]);
                luminanceStagingUsed[currentLuminanceIndex] = true;

                // Read-back to CPU using a ring of staging buffers
                for (int i = luminancesStaging.Length - 1; i >= 1; i--)
                {
                    var oldStagingIndex = (currentLuminanceIndex + i) % luminancesStaging.Length;
                    var oldLuminanceStaging = luminancesStaging[oldStagingIndex];
                    if (luminanceStagingUsed[oldStagingIndex] && oldLuminanceStaging.GetData(mapValue, 0, 0, true))
                    {
                        AverageLuminance = (float)Math.Pow(2.0, mapValue[0]);
                        //Debug.WriteLine(string.Format("Buffer {0}:{1} Lum: {2}", currentLuminanceIndex, oldStagingIndex, AverageLuminance));
                        break;
                    }
                }
                currentLuminanceIndex = (currentLuminanceIndex + 1) % luminancesStaging.Length;
            }

            // Upscale only if output is larger
            if (upscaleBaseIndex > 1 && UpscaleCount > 0)
            {
                var lastRenderTarget = tempMipmaps[upscaleBaseIndex];
                blur.SetInput(lastRenderTarget);
                blur.SetOutput(lastRenderTarget);
                blur.Draw();
                blur.Draw();

                for (int j = 1; j < UpscaleCount; j++)
                {
                    Scaler.SetInput(lastRenderTarget);
                    lastRenderTarget = tempMipmaps[upscaleBaseIndex - j];
                    Scaler.SetOutput(lastRenderTarget);
                    Scaler.Draw("Upx2");
                }
                Scaler.SetInput(lastRenderTarget);
                Scaler.SetOutput(outputRenderTexture);
                Scaler.Draw("Upx2Last");
            }
        }
    }
}