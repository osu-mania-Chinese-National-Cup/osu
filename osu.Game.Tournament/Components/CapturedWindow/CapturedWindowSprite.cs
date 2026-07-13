// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Veldrid;
using osu.Framework.Graphics.Veldrid.Textures;
using osu.Framework.Logging;
using osu.Game.Tournament.Models;
using SixLabors.ImageSharp.PixelFormats;
using Vortice.Direct3D11;
using FillMode = osu.Framework.Graphics.FillMode;

namespace osu.Game.Tournament.Components.CapturedWindow
{
    [SupportedOSPlatform("windows10.0.26100.0")]
    public partial class CapturedWindowSprite : CompositeDrawable
    {
        private Sprite sprite = null!;
        private CaptureTarget? captureTarget;
        private ICaptureSource? capture;
        private D3D11ExternalTexture? externalTexture;
        private Texture? cpuTexture;

        // ReSharper disable once InconsistentNaming
        private bool d3d11Available;
        private Texture? pendingSpriteTexture;
        private volatile bool spriteTextureAssignmentsStopped;

        public int Index { get; }

        [Resolved]
        private LadderInfo? ladder { get; set; }

        [Resolved]
        private TournamentCaptureManager manager { get; set; } = null!;

        public CapturedWindowSprite(int index)
        {
            Index = index;
            Masking = true;
            AlwaysPresent = true;
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit
            };

            AddInternal(sprite);

            if (ladder != null)
            {
                FrameRate.BindTo(ladder.FrameRate);
            }

            d3d11Available = D3D11Interop.TryGetD3D11Device(renderer, out var device, out _, out _);

            if (d3d11Available)
                capture = new WgcCaptureSource(new WgcCapture(device!));
            else
                capture = new BitBltCaptureSource();

            manager.RegisterCapture(this);
        }

        public void SetTarget(CaptureTarget? newTarget) => Scheduler.Add(() =>
        {
            if (captureTarget == newTarget)
                return;

            captureTarget = newTarget;

            Name = $"WindowCapture<{captureTarget?.ProcessId}>";

            if (capture == null)
                return;

            if (captureTarget == null)
            {
                capture.Stop();
                return;
            }

            if (capture.IsRunning)
            {
                capture.Stop();
                sprite.Texture = null;
                externalTexture = null;
                cpuTexture = null;
            }

            if (IsWindow(captureTarget.WindowHandle))
            {
                try
                {
                    capture.StartForWindow(captureTarget.WindowHandle);
                    captureTarget.IsAlive = true;
                    captureErrorReported = false;
                }
                catch (Exception e)
                {
                    if (!captureErrorReported)
                    {
                        Logger.Error(e, $"{nameof(CapturedWindowSprite)}: Process Id {captureTarget.ProcessId} Capture Error");
                        captureErrorReported = true;
                    }

                    captureTarget.IsAlive = false;
                }
            }
            else
            {
                captureTarget.IsAlive = false;
            }
        });

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public BindableInt FrameRate { get; } = new BindableInt(60)
        {
            MinValue = 30,
            MaxValue = 360,
            Default = 60,
        };

        private bool captureErrorReported;

        private double aliveCheckElapsed;

        protected override void Update()
        {
            base.Update();

            if (capture == null)
                return;

            if (captureTarget == null)
            {
                this.FadeOut(100);
                return;
            }

            aliveCheckElapsed += Clock.ElapsedFrameTime;

            if (aliveCheckElapsed > 500)
            {
                if (captureTarget.IsAlive &&
                    !IsWindow(captureTarget.WindowHandle))
                {
                    captureTarget.IsAlive = false;

                    if (capture?.IsRunning == true)
                        capture.Stop();

                    sprite.Texture = null;
                    externalTexture = null;
                    cpuTexture = null;
                }

                aliveCheckElapsed = 0;
            }

            if (!captureTarget.IsAlive)
            {
                this.FadeOut(100);
            }
            else
            {
                this.FadeIn(100);
            }
        }

        private void consumePendingFrame(CaptureFrame frame, IRenderer renderer)
        {
            if (capture == null || !frame.IsValid)
                return;

            bool resourceOwnershipTransferred = false;

            try
            {
                Texture? textureToApply = null;
                resourceOwnershipTransferred = capture.ApplyFrame(frame, renderer, ref externalTexture, ref cpuTexture, out textureToApply);

                if (textureToApply != null)
                    queueSpriteTexture(textureToApply);
            }
            finally
            {
                frame.ReleaseResources(discardUpload: !resourceOwnershipTransferred);
            }
        }

        private void queueSpriteTexture(Texture texture)
        {
            if (spriteTextureAssignmentsStopped)
            {
                texture.Dispose();
                return;
            }

            var previousTexture = Interlocked.Exchange(ref pendingSpriteTexture, texture);

            if (previousTexture != null && previousTexture != texture)
                previousTexture.Dispose();

            if (spriteTextureAssignmentsStopped)
            {
                Interlocked.Exchange(ref pendingSpriteTexture, null)?.Dispose();
                return;
            }

            Scheduler.AddOnce(applyPendingSpriteTexture);
        }

        private void applyPendingSpriteTexture()
        {
            var texture = Interlocked.Exchange(ref pendingSpriteTexture, null);

            if (texture == null)
                return;

            if (spriteTextureAssignmentsStopped)
            {
                texture.Dispose();
                return;
            }

            sprite.Texture = texture;
        }

        protected override DrawNode CreateDrawNode() => new CaptureDrawNode(this);

        private sealed class CaptureDrawNode : CompositeDrawableDrawNode
        {
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            private double elapsedMs;

            public CaptureDrawNode(CapturedWindowSprite source)
                : base(source)
            {
            }

            protected override void Draw(IRenderer renderer)
            {
                var source = (CapturedWindowSprite)Source;
                double interval = 1000.0 / source.FrameRate.Value;
                elapsedMs += stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Restart();

                var capture = source.capture;

                if (!source.spriteTextureAssignmentsStopped && capture != null && elapsedMs >= interval)
                {
                    if (capture.TryAcquireLatestFrame(out var frame))
                        source.consumePendingFrame(frame, renderer);

                    elapsedMs = Math.Min(elapsedMs - interval, interval);
                }

                base.Draw(renderer);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            var captureToDispose = capture;
            capture = null;
            spriteTextureAssignmentsStopped = true;

            Interlocked.Exchange(ref pendingSpriteTexture, null)?.Dispose();

            base.Dispose(isDisposing);
            captureToDispose?.Dispose();
            externalTexture?.Dispose();
            cpuTexture?.Dispose();
        }

        #region Windows API

        // ReSharper disable InconsistentNaming
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
                                          IntPtr hdcSrc, int xSrc, int ySrc, int rop);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static IntPtr FindWindowByPartialTitle(string partialTitle)
        {
            IntPtr result = FindWindow(null, partialTitle);

            if (result != IntPtr.Zero)
                return result;

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);

                if (sb.ToString().Contains(partialTitle))
                {
                    result = hWnd;
                    return false; // 停止遍历
                }

                return true;
            }, IntPtr.Zero);

            return result;
        }
        // ReSharper restore InconsistentNaming

        #endregion

        private interface ICaptureSource : IDisposable
        {
            bool IsRunning { get; }
            void StartForWindow(IntPtr hwnd);
            void Stop();
            bool TryAcquireLatestFrame(out CaptureFrame frame);
            bool ApplyFrame(CaptureFrame frame, IRenderer renderer, ref D3D11ExternalTexture? externalTexture, ref Texture? cpuTexture, out Texture? textureToApply);
        }

        private readonly struct CaptureFrame
        {
            public static readonly CaptureFrame EMPTY = new CaptureFrame(CaptureFrameKind.None, null, null, 0, 0);

            public CaptureFrameKind Kind { get; }
            public ID3D11Texture2D? D3D11Texture { get; }
            public ITextureUpload? Upload { get; }
            public int Width { get; }
            public int Height { get; }

            public bool IsValid => Kind != CaptureFrameKind.None;

            private CaptureFrame(CaptureFrameKind kind, ID3D11Texture2D? texture, ITextureUpload? upload, int width, int height)
            {
                Kind = kind;
                D3D11Texture = texture;
                Upload = upload;
                Width = width;
                Height = height;
            }

            public static CaptureFrame FromD3D11(ID3D11Texture2D texture, int width, int height)
                => new CaptureFrame(CaptureFrameKind.D3D11Texture, texture, null, width, height);

            public static CaptureFrame FromUpload(ITextureUpload upload, int width, int height)
                => new CaptureFrame(CaptureFrameKind.CpuUpload, null, upload, width, height);

            public void ReleaseResources(bool discardUpload)
            {
                D3D11Texture?.Release();

                if (discardUpload)
                    Upload?.Dispose();
            }
        }

        private enum CaptureFrameKind
        {
            None,
            D3D11Texture,
            CpuUpload
        }

        private sealed class WgcCaptureSource : ICaptureSource
        {
            private readonly WgcCapture capture;

            public WgcCaptureSource(WgcCapture capture)
            {
                this.capture = capture;
            }

            public bool IsRunning => capture.IsRunning;

            public void StartForWindow(IntPtr hwnd) => capture.StartForWindow(hwnd);

            public void Stop() => capture.Stop();

            public bool TryAcquireLatestFrame(out CaptureFrame frame)
            {
                if (capture.TryAcquireLatestTexture(out var texture, out int width, out int height))
                {
                    frame = CaptureFrame.FromD3D11(texture, width, height);
                    return true;
                }

                frame = CaptureFrame.EMPTY;
                return false;
            }

            public bool ApplyFrame(CaptureFrame frame, IRenderer renderer, ref D3D11ExternalTexture? externalTexture, ref Texture? cpuTexture, out Texture? textureToApply)
            {
                textureToApply = null;

                if (frame.Kind != CaptureFrameKind.D3D11Texture || frame.D3D11Texture == null)
                    return false;

                if (externalTexture == null || externalTexture.Width != frame.Width || externalTexture.Height != frame.Height)
                {
                    externalTexture = new D3D11ExternalTexture(renderer, frame.Width, frame.Height);
                    textureToApply = externalTexture;
                }

                externalTexture.UpdateFrom(frame.D3D11Texture);
                return false;
            }

            public void Dispose() => capture.Dispose();
        }

        private sealed class BitBltCaptureSource : ICaptureSource
        {
            private System.Drawing.Bitmap? bitmapPool;
            private System.Drawing.Graphics? graphicsPool;
            private byte[]? rawBufferPool;
            private int poolWidth;
            private int poolHeight;
            private int poolStride;
            private IntPtr hwnd;

            public bool IsRunning { get; private set; }

            public void StartForWindow(IntPtr hwnd)
            {
                this.hwnd = hwnd;
                IsRunning = true;
            }

            public void Stop()
            {
                IsRunning = false;
                hwnd = IntPtr.Zero;
            }

            public bool TryAcquireLatestFrame(out CaptureFrame frame)
            {
                frame = CaptureFrame.EMPTY;

                if (!IsRunning)
                    return false;

                if (hwnd == IntPtr.Zero)
                    return false;

                if (!GetWindowRect(hwnd, out RECT rect))
                    return false;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (width <= 0 || height <= 0)
                    return false;

                if (bitmapPool == null || graphicsPool == null || poolWidth != width || poolHeight != height)
                {
                    bitmapPool?.Dispose();
                    graphicsPool?.Dispose();

                    bitmapPool = new System.Drawing.Bitmap(width, height, PixelFormat.Format24bppRgb);
                    graphicsPool = System.Drawing.Graphics.FromImage(bitmapPool);

                    var tmpData = bitmapPool.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format24bppRgb);
                    poolStride = Math.Abs(tmpData.Stride);
                    bitmapPool.UnlockBits(tmpData);

                    rawBufferPool = new byte[poolStride * height];

                    poolWidth = width;
                    poolHeight = height;
                }

                try
                {
                    IntPtr hdcDest = graphicsPool.GetHdc();
                    IntPtr hdcSrc = GetWindowDC(hwnd);
                    BitBlt(hdcDest, 0, 0, width, height, hdcSrc, 0, 0, 0x00CC0020);
                    graphicsPool.ReleaseHdc(hdcDest);
                    ReleaseDC(hwnd, hdcSrc);

                    var bmpData = bitmapPool.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format24bppRgb);

                    Marshal.Copy(bmpData.Scan0, rawBufferPool!, 0, rawBufferPool!.Length);
                    bitmapPool.UnlockBits(bmpData);

                    var upload = new ArrayPoolTextureUpload(width, height);
                    convertRgr24ToRgba32(rawBufferPool!, width, height, poolStride, upload.RawData);

                    frame = CaptureFrame.FromUpload(upload, width, height);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static void convertRgr24ToRgba32(byte[] src, int width, int height, int stride, Span<Rgba32> dst)
            {
                int dstIdx = 0;

                for (int y = 0; y < height; y++)
                {
                    int rowStart = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        int i = rowStart + x * 3;
                        byte b = src[i + 0];
                        byte g = src[i + 1];
                        byte r = src[i + 2];

                        dst[dstIdx++] = new Rgba32(r, g, b, 255);
                    }
                }
            }

            public bool ApplyFrame(CaptureFrame frame, IRenderer renderer, ref D3D11ExternalTexture? externalTexture, ref Texture? cpuTexture, out Texture? textureToApply)
            {
                textureToApply = null;

                if (frame.Kind != CaptureFrameKind.CpuUpload || frame.Upload == null)
                    return false;

                if (cpuTexture == null || cpuTexture.Width != frame.Width || cpuTexture.Height != frame.Height)
                {
                    cpuTexture = renderer.CreateTexture(frame.Width, frame.Height);
                    textureToApply = cpuTexture;
                }

                cpuTexture.SetData(frame.Upload);
                return true;
            }

            public void Dispose()
            {
                bitmapPool?.Dispose();
                graphicsPool?.Dispose();
            }
        }
    }
}
