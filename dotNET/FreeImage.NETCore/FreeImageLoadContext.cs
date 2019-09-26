// ==========================================================
// FreeImage 3 .NET wrapper
// Original FreeImage 3 functions and .NET compatible derived functions
//
// Design and implementation by
// - Jean-Philippe Goerke (jpgoerke@users.sourceforge.net)
// - Carsten Klein (cklein05@users.sourceforge.net)
//
// Contributors:
// - David Boland (davidboland@vodafone.ie)
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

// ==========================================================
// CVS
// $Revision: 1.9 $
// $Date: 2009/09/15 11:41:37 $
// $Id: FreeImageStaticImports.cs,v 1.9 2009/09/15 11:41:37 cklein05 Exp $
// ==========================================================

using System;
using System.Runtime.InteropServices;
//using System.Runtime.Loader;
using System.Reflection;
using System.IO;

namespace FreeImageAPI
{
    //public class FreeImageLoadContext : AssemblyLoadContext
    //{
    //    protected override Assembly Load(AssemblyName assemblyName)
    //    {
    //        // Return null to fallback on default load context
    //        return null;
    //    }
    //    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    //    {
    //        if (unmanagedDllName == "libFreeImage")
    //        {
    //            string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
    //            string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    //            // Environment.OSVersion.Platform returns "Unix" for Unix or OSX, so use RuntimeInformation here
    //            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    //            var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    //            var isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    //            var prefix = isWindows ? "" : isLinux ? "lib" : "lib";
    //            var ext = isWindows ? "dll" : isLinux ? "so" : "dylib";
    //            var os = isWindows ? "win" : isLinux ? "linux" : "osx";
    //            var fileName = Path.Combine(assemblyDirectory, $"/runtimes/{os}-{RuntimeInformation.OSArchitecture}/native/{prefix}FreeImage.{ext}");
    //            if (File.Exists(fileName))
    //                return LoadUnmanagedDllFromPath(fileName);

    //        }
    //        return IntPtr.Zero;
    //    }
    //}
}