// ==========================================================
// FreeImage 3 .NET wrapper
// Original FreeImage 3 functions and .NET compatible derived functions
//
// Design and implementation by
// - Jean-Philippe Goerke (jpgoerke@users.sourceforge.net)
// - Carsten Klein (cklein05@users.sourceforge.net)
//
// Contributors:

// Main reference : MSDN Knowlede Base
//
// This file is part of FreeImage 3
//
// COVERED CODE IS PROVIDED UNDER THIS LICENSE ON AN "AS IS" BASIS, WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING, WITHOUT LIMITATION, WARRANTIES
// THAT THE COVERED CODE IS FREE OF DEFECTS, MERCHANTABLE, FIT FOR A PARTICULAR PURPOSE
// OR NON-INFRINGING. THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE OF THE COVERED
// CODE IS WITH YOU. SHOULD ANY COVERED CODE PROVE DEFECTIVE IN ANY RESPECT, YOU (NOT
// THE INITIAL DEVELOPER OR ANY OTHER CONTRIBUTOR) ASSUME THE COST OF ANY NECESSARY
// SERVICING, REPAIR OR CORRECTION. THIS DISCLAIMER OF WARRANTY CONSTITUTES AN ESSENTIAL
// PART OF THIS LICENSE. NO USE OF ANY COVERED CODE IS AUTHORIZED HEREUNDER EXCEPT UNDER
// THIS DISCLAIMER.
//
// Use at your own risk!
// ==========================================================

namespace System.Drawing.Imaging
{
    public enum ImageFlags
    {
        Caching = 131072,//The pixel data can be cached for faster access.
        ColorSpaceCmyk = 32,//The pixel data uses a CMYK color space.
        ColorSpaceGray = 64,//The pixel data is grayscale.
        ColorSpaceRgb = 16,//The pixel data uses an RGB color space.
        ColorSpaceYcbcr = 128,//Specifies that the image is stored using a YCBCR color space.
        ColorSpaceYcck = 256,//Specifies that the image is stored using a YCCK color space.
        HasAlpha = 2,//The pixel data contains alpha information.
        HasRealDpi = 4096,//Specifies that dots per inch information is stored in the image.
        HasRealPixelSize = 8192,//Specifies that the pixel size is stored in the image.
        HasTranslucent = 4,//Specifies that the pixel data has alpha values other than 0 (transparent) and 255 (opaque).
        None = 0,//There is no format information.
        PartiallyScalable = 8,//The pixel data is partially scalable, but there are some limitations.
        ReadOnly = 65536,//The pixel data is read-only.
        Scalable = 1 //The pixel data is scalable.
    }
}