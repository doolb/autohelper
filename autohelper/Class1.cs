using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class MouseOperations
{
    [Flags]
    public enum MouseEventFlags
    {
        LeftDown = 0x00000002,
        LeftUp = 0x00000004,
        MiddleDown = 0x00000020,
        MiddleUp = 0x00000040,
        Move = 0x00000001,
        Absolute = 0x00008000,
        RightDown = 0x00000008,
        RightUp = 0x00000010
    }

    [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out MousePoint lpMousePoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    public static void SetCursorPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    public static void SetCursorPosition(MousePoint point)
    {
        SetCursorPos(point.X, point.Y);
    }

    public static MousePoint GetCursorPosition()
    {
        MousePoint currentMousePoint;
        var gotPoint = GetCursorPos(out currentMousePoint);
        if (!gotPoint) { currentMousePoint = new MousePoint(0, 0); }
        return currentMousePoint;
    }

    public static void MouseEvent(MouseEventFlags value)
    {
        MousePoint position = GetCursorPosition();

        mouse_event
            ((int)value,
                position.X,
                position.Y,
                0,
                0)
            ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MousePoint
    {
        public int X;
        public int Y;

        public MousePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}

namespace autohelper
{
    internal partial class Program
    {
        public static Mat makeScreenshot()
        {
            if (adb)
                return makeScreenshotAdb();
            else
                return BitmapConverter.ToMat(makeScreenshotbmp());
        }

        public static Bitmap makeScreenshotbmp()
        {
            Bitmap screenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);

            Graphics gfxScreenshot = Graphics.FromImage(screenshot);

            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            gfxScreenshot.Dispose();

            return screenshot;
        }

        public static bool adb = false;
        private static string adbport = "";

        private static void initAdb(bool _screenshotadb)
        {
            adb = true;
        }

        public static Mat makeScreenshotAdb()
        {
            Process pr2 = new Process();
            pr2.StartInfo.FileName = @"c:\windows\system32\cmd.exe";
            pr2.StartInfo.Arguments = $"/c \"adb.exe {adbport}exec-out screencap -p > adb.png\"";
            pr2.Start();
            pr2.WaitForExit();

            return Cv2.ImRead("adb.png");

            var outputStream = new StreamWriter("adb.png");
            Process process = new Process();
            process.StartInfo.FileName = "adb.exe";
            process.StartInfo.Arguments = "exec-out screencap -p";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    outputStream.WriteLine(e.Data);
                }
            });

            process.Start();

            process.BeginOutputReadLine();

            process.WaitForExit();
            process.Close();

            outputStream.Close();

            return Cv2.ImRead("adb.png");
        }

        private static int adb_x, adb_y;
        public static void SetCursorPosition(int x, int y)
        {
            if (adb)
            {
                adb_x = x;
                adb_y = y;
            }
            else
            {
                MouseOperations.SetCursorPosition(x, y);
            }
        }
        public static void MouseEvent(MouseOperations.MouseEventFlags value)
        {
            if (adb)
            {
                if (value == MouseOperations.MouseEventFlags.LeftUp)
                {
                    double scale = 1;
                    Process.Start(new ProcessStartInfo("adb.exe", $"{adbport}shell input tap {adb_x * scale} {adb_y * scale}"))?.WaitForExit(-1);
                }
            }
            else
            {
                MouseOperations.MouseEvent(value);
            }
        }
    }
}
