// Copyright (c) 2014-2017 Silicon Studio Corp. All rights reserved. (https://www.siliconstudio.co.jp)
// See LICENSE.md for full license information.

using System;

namespace SiliconStudio.Xenko.Rendering.Shadows
{
    [Flags]
    public enum LightShadowType : ushort // DO NOT CHANGE the size of this type. It is used to caculate the shaderKeyId in LightComponentForwardRenderer. 
    {
        Cascade1 = 0x1,
        Cascade2 = 0x2,
        Cascade4 = 0x3,
        
        CascadeMask = 0x3,

        Debug = 0x4,

        BlendCascade = 0x8,

        DepthRangeAuto = 0x10,

        FilterMask = 0xF00,

        PCF3x3 = 0x100,

        PCF5x5 = 0x200,

        PCF7x7 = 0x300
    }
}
