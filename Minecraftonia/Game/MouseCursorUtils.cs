using System;
using System.Runtime.InteropServices;
using Avalonia;

namespace Minecraftonia.Game;

internal static partial class MouseCursorUtils
{
    public static bool TryWarpPointer(PixelPoint screenPoint)
    {
        if (OperatingSystem.IsWindows())
        {
            return SetCursorPos(screenPoint.X, screenPoint.Y);
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryWarpMac(screenPoint);
        }

        return false;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    private static bool TryWarpMac(PixelPoint screenPoint)
    {
        var displayId = CGMainDisplayID();
        if (displayId == IntPtr.Zero)
        {
            return false;
        }

        var bounds = CGDisplayBounds(displayId);
        double targetX = screenPoint.X;
        double targetY = bounds.Size.Height - screenPoint.Y;
        var target = new CGPoint { X = targetX, Y = targetY };

        CGWarpMouseCursorPosition(target);
        CGAssociateMouseAndMouseCursorPosition(true);
        return true;
    }

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial void CGWarpMouseCursorPosition(CGPoint position);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial void CGAssociateMouseAndMouseCursorPosition([MarshalAs(UnmanagedType.Bool)] bool connected);

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial nint CGMainDisplayID();

    [LibraryImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static partial CGRect CGDisplayBounds(nint display);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public CGPoint Origin;
        public CGSize Size;
    }
}
