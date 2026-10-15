using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MakiseDesktopPet
{
    internal class LayeredForm : Form
    {
        private readonly bool clickThrough;
        private Bitmap currentFrame;
        private IntPtr frameDc;
        private IntPtr frameBitmap;
        private IntPtr framePixels;
        private IntPtr originalBitmap;
        private Size bufferSize;
        private byte[] transfer;

        internal LayeredForm(bool clickThrough)
        {
            this.clickThrough = clickThrough;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ControlBox = false;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message message)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            if (message.Msg == WM_MOUSEACTIVATE)
            {
                message.Result = new IntPtr(MA_NOACTIVATE);
                return;
            }
            base.WndProc(ref message);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= NativeMethods.WS_EX_LAYERED |
                    NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
                if (clickThrough)
                    parameters.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                return parameters;
            }
        }

        internal void SetFrame(Bitmap frame)
        {
            if (frame == null || frame.PixelFormat != PixelFormat.Format32bppPArgb)
                throw new ArgumentException("Layered frames must be premultiplied 32-bit bitmaps.");

            currentFrame = frame;
            Size = frame.Size;
            if (IsHandleCreated)
                UploadFrame();
        }

        internal void PlaceAtBottom()
        {
            if (!Visible || !IsHandleCreated) return;
            if (!NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_BOTTOM, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (currentFrame != null)
                UploadFrame();
        }

        private void ReleaseBuffer()
        {
            if (originalBitmap != IntPtr.Zero) NativeMethods.SelectObject(frameDc, originalBitmap);
            if (frameBitmap != IntPtr.Zero) NativeMethods.DeleteObject(frameBitmap);
            if (frameDc != IntPtr.Zero) NativeMethods.DeleteDC(frameDc);
            frameDc = frameBitmap = framePixels = originalBitmap = IntPtr.Zero;
            transfer = null;
            bufferSize = Size.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            ReleaseBuffer();
            base.Dispose(disposing);
        }

        private void EnsureBuffer(IntPtr screen)
        {
            if (frameDc != IntPtr.Zero && bufferSize == currentFrame.Size) return;
            ReleaseBuffer();
            frameDc = NativeMethods.CreateCompatibleDC(screen);
            NativeMethods.BitmapInfo information = new NativeMethods.BitmapInfo();
            information.Header.Size = (uint)Marshal.SizeOf(typeof(NativeMethods.BitmapInfoHeader));
            information.Header.Width = currentFrame.Width;
            information.Header.Height = -currentFrame.Height;
            information.Header.Planes = 1;
            information.Header.BitCount = 32;
            frameBitmap = NativeMethods.CreateDIBSection(screen, ref information, 0,
                out framePixels, IntPtr.Zero, 0);
            if (frameDc == IntPtr.Zero || frameBitmap == IntPtr.Zero || framePixels == IntPtr.Zero)
            {
                ReleaseBuffer();
                throw new Win32Exception("Unable to create a layered-window bitmap.");
            }
            originalBitmap = NativeMethods.SelectObject(frameDc, frameBitmap);
            bufferSize = currentFrame.Size;
            transfer = new byte[currentFrame.Width * currentFrame.Height * 4];
        }

        private void UploadFrame()
        {
            IntPtr screen = IntPtr.Zero;
            try
            {
                screen = NativeMethods.GetDC(IntPtr.Zero);
                if (screen == IntPtr.Zero) throw new Win32Exception();
                EnsureBuffer(screen);

                Rectangle area = new Rectangle(0, 0, currentFrame.Width, currentFrame.Height);
                BitmapData data = currentFrame.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    int stride = currentFrame.Width * 4;
                    for (int y = 0; y < currentFrame.Height; y++)
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), transfer, y * stride, stride);
                    Marshal.Copy(transfer, 0, framePixels, transfer.Length);
                }
                finally { currentFrame.UnlockBits(data); }

                NativeMethods.Point destination = new NativeMethods.Point(Left, Top);
                NativeMethods.Size size = new NativeMethods.Size(currentFrame.Width, currentFrame.Height);
                NativeMethods.Point source = new NativeMethods.Point(0, 0);
                NativeMethods.BlendFunction blend = new NativeMethods.BlendFunction();
                blend.BlendOp = NativeMethods.AC_SRC_OVER;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = NativeMethods.AC_SRC_ALPHA;
                if (!NativeMethods.UpdateLayeredWindow(Handle, screen, ref destination,
                    ref size, frameDc, ref source, 0, ref blend, NativeMethods.ULW_ALPHA))
                    throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            }
            finally
            {
                if (screen != IntPtr.Zero)
                    NativeMethods.ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }
}
