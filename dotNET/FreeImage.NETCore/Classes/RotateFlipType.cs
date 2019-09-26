// ==========================================================
// FreeImage 3 .NET wrapper
// Original FreeImage 3 functions and .NET compatible derived functions
//
// Design and implementation by
// - Jean-Philippe Goerke (jpgoerke@users.sourceforge.net)
// - Carsten Klein (cklein05@users.sourceforge.net)
//
// Contributors:
//
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
    public enum RotateFlipType
    {
        Rotate180FlipNone = 2,//Specifies a 180-degree clockwise rotation without flipping.
        Rotate180FlipX = 6,//Specifies a 180-degree clockwise rotation followed by a horizontal flip.
        Rotate180FlipXY = 0,//Specifies a 180-degree clockwise rotation followed by a horizontal and vertical flip.
        Rotate180FlipY = 4,//Specifies a 180-degree clockwise rotation followed by a vertical flip.
        Rotate270FlipNone = 3,//Specifies a 270-degree clockwise rotation without flipping.
        Rotate270FlipX = 7,//Specifies a 270-degree clockwise rotation followed by a horizontal flip.
        Rotate270FlipXY = 1,//Specifies a 270-degree clockwise rotation followed by a horizontal and vertical flip.
        Rotate270FlipY = 5,//Specifies a 270-degree clockwise rotation followed by a vertical flip.
        Rotate90FlipNone = 1,//Specifies a 90-degree clockwise rotation without flipping.
        Rotate90FlipX = 5,//Specifies a 90-degree clockwise rotation followed by a horizontal flip.
        Rotate90FlipXY = 3,//Specifies a 90-degree clockwise rotation followed by a horizontal and vertical flip.
        Rotate90FlipY = 7,//Specifies a 90-degree clockwise rotation followed by a vertical flip.
        RotateNoneFlipNone = 0,//Specifies no clockwise rotation and no flipping.
        RotateNoneFlipX = 4,//Specifies no clockwise rotation followed by a horizontal flip.
        RotateNoneFlipXY = 2,//Specifies no clockwise rotation followed by a horizontal and vertical flip.
        RotateNoneFlipY = 6,//Specifies no clockwise rotation followed by a vertical flip.
    }
}