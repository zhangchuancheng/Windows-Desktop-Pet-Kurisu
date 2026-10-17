using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MakiseDesktopPet
{
    internal sealed class PetForm : LayeredForm
    {
        private const int SleepAfterMs = 180000;
        private const int BubbleDurationMs = 5000;
        private const int DragThresholdPx = 6;
        private const int RapidReleaseMs = 1200;
        private static readonly Pose[] ClickPoses =
            { Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek };
        private readonly SettingsStore settingsStore;
        private readonly PetSettings settings;
        private readonly SpriteAssets sprites;
        private readonly Dialogues dialogues;
        private readonly BubbleForm bubble;
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly PetMenuPopup petMenu;
        private readonly ContextMenuStrip trayMenu;
        private readonly Timer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private readonly Queue<long> recentClicks = new Queue<long>();
        private readonly PetMotion motion = new PetMotion();
        private readonly SleepAnimation sleepAnimation = new SleepAnimation();
        private readonly DragWarpAnimation dragAnimation;
        private readonly LayeredIdleAnimation layeredIdle;
        private readonly GazeMotion gaze = new GazeMotion();

        private Pose pose = Pose.Idle;
        private int nextClickPose;
        private bool sleeping;
        private bool pressed;
        private bool dragging;
        private long dragStarted;
        private int baseX;
        private int baseY;
        private Point pressPoint;
        private Point pressBase;
        private Point lastDragTarget;
        private long lastInteraction;
        private long sleepStarted;
        private long poseUntil;
        private long blinkUntil;
        private long blinkStarted;
        private long nextBlink;
        private bool doubleBlink;
        private long bubbleUntil;
        private long specialUntil;
        private long rapidCooldownUntil;
        private long rapidPoseUntil;
        private string bubbleText;
        private BubblePlacement bubblePlacement;

        internal PetForm(string directory)
            : base(false)
        {
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            sprites = new SpriteAssets(System.IO.Path.Combine(directory, "assets", "sprites"), settings.Height);
            dragAnimation = new DragWarpAnimation(System.IO.Path.Combine(directory, "assets", "sprites"), sprites.Width, sprites.Height);
            layeredIdle = new LayeredIdleAnimation(System.IO.Path.Combine(directory, "assets", "layers", "idle-v1"), settings.Height);
            dialogues = new Dialogues(System.IO.Path.Combine(directory, "dialogues.json"));
            bubble = new BubbleForm();
            TopMost = settings.TopMost;
            bubble.TopMost = settings.TopMost;

            if (!settings.HasPosition)
            {
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                settings.X = work.Right - sprites.Width - 24;
                settings.Y = work.Bottom - sprites.Height - 16;
                settings.HasPosition = true;
            }
            baseX = settings.X;
            baseY = settings.Y;
            ClampToVisibleScreen();
            Location = new Point(baseX, baseY);
            layeredIdle.Enter(clock.ElapsedMilliseconds);
            RenderMotion(clock.ElapsedMilliseconds, true);

            petMenu = BuildPetMenu();
            trayMenu = BuildTrayMenu();
            trayIcon = CreateTrayIcon(directory);
            Icon = trayIcon;
            tray = new NotifyIcon();
            tray.Icon = trayIcon;
            tray.Text = "牧濑红莉栖桌宠";
            tray.ContextMenuStrip = trayMenu;
            tray.DoubleClick += delegate { RestoreFromTray(); };
            tray.Visible = true;

            ScheduleBlink(clock.ElapsedMilliseconds);
            timer = new Timer();
            timer.Interval = 16;
            timer.Tick += OnTimerTick;
            timer.Start();
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        private PetMenuPopup BuildPetMenu()
        {
            return new PetMenuPopup(
                delegate(int height) { ApplySize(height); }, SaveSettings, ChangeLayer,
                ChangeUiScale, ChangeDialogue, HideToTray, Close, KeepAtBottom);
        }

        private void ChangeUiScale(int percent)
        {
            if (settings.UiScalePercent == percent) return;
            settings.UiScalePercent = percent;
            SaveSettings();
        }

        private void ChangeLayer(bool topmost)
        {
            Interact();
            settings.TopMost = topmost;
            ApplyWindowLayer();
            SaveSettings();
        }

        private void ApplyWindowLayer()
        {
            TopMost = settings.TopMost;
            bubble.TopMost = settings.TopMost;
            KeepAtBottom();
        }

        private void KeepAtBottom()
        {
            if (settings.TopMost) return;
            if (bubble.Visible) bubble.PlaceAtBottom();
            PlaceAtBottom();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyWindowLayer();
        }

        private ContextMenuStrip BuildTrayMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem visibility = new ToolStripMenuItem("隐藏");
            visibility.Click += delegate
            {
                if (Visible) HideToTray();
                else RestoreFromTray();
            };
            menu.Items.Add(visibility);
            menu.Items.Add("退出", null, delegate { Close(); });
            menu.Opening += delegate { visibility.Text = Visible ? "隐藏" : "显示"; };
            return menu;
        }

        private static Icon CreateTrayIcon(string directory)
        {
            string path = System.IO.Path.Combine(directory, "assets", "icons", "app.ico");
            return new Icon(path, SystemInformation.SmallIconSize);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Interact();
            pressed = true;
            dragging = false;
            pressPoint = PhysicalMousePosition();
            pressBase = new Point(baseX, baseY);
            lastDragTarget = pressBase;
            Capture = true;
            if (pose == Pose.Blink)
            {
                SetPose(Pose.Idle);
                ScheduleBlink(clock.ElapsedMilliseconds);
            }
            if (sleeping)
            {
                sleeping = false;
                SetPose(Pose.Wake);
                poseUntil = clock.ElapsedMilliseconds + 850;
                ScheduleBlink(clock.ElapsedMilliseconds);
            }
            KeepAtBottom();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pressed) return;
            Point pointer = PhysicalMousePosition();
            int dx = pointer.X - pressPoint.X;
            int dy = pointer.Y - pressPoint.Y;
            if (!dragging && dx * dx + dy * dy >= DragThresholdPx * DragThresholdPx)
            {
                dragging = true;
                recentClicks.Clear();
                poseUntil = 0;
                motion.CancelRecoil();
                bubble.Hide();
                bubbleUntil = 0;
                SetPose(Pose.Drag);
            }
            if (dragging)
            {
                Interact();
                UpdateDragPosition(pointer);
            }
        }

        private void UpdateDragPosition(Point pointer)
        {
            dragAnimation.ObservePointer(pointer);
            int targetX = pressBase.X + pointer.X - pressPoint.X;
            int targetY = pressBase.Y + pointer.Y - pressPoint.Y;
            if (targetX == lastDragTarget.X && targetY == lastDragTarget.Y) return;
            lastDragTarget = new Point(targetX, targetY);
            baseX = targetX;
            baseY = targetY;
            ClampToVisibleScreen();
            Point next = new Point(baseX, baseY);
            if (Location != next) Location = next;
        }

        private Bitmap CurrentFrame()
        {
            return sprites.Get(pose);
        }

        private void RenderMotion(long now, bool force)
        {
            if (pose == Pose.Drag)
            {
                bool dragChanged;
                Bitmap dragFrame = dragAnimation.Render(now - dragStarted, out dragChanged);
                if (dragChanged || force) SetFrame(dragFrame);
                return;
            }
            if (pose == Pose.Idle || pose == Pose.Blink)
            {
                int eyes = pose == Pose.Blink ? LayeredIdleAnimation.EyeForBlink(now, blinkStarted, blinkUntil) : 0;
                bool updated;
                Bitmap idleFrame = layeredIdle.Render(now, eyes,
                    gaze.EyeX, gaze.EyeY, gaze.HeadX, gaze.HeadY, out updated);
                if (updated || force) SetFrame(idleFrame);
                return;
            }
            if (pose == Pose.Sleep)
            {
                bool updated;
                Bitmap sleepFrame = sleepAnimation.Render(sprites.Get(Pose.Sleep),
                    now - sleepStarted, out updated);
                if (updated || force) SetFrame(sleepFrame);
                return;
            }
            bool animated = pose == Pose.PokeBlock || pose == Pose.PokeFold || pose == Pose.PokeCheek;
            if (!animated)
            {
                if (force) SetFrame(CurrentFrame());
                return;
            }
            bool changed;
            Bitmap frame = motion.Render(CurrentFrame(), motion.Displacement(now, sprites.Height), out changed);
            if (changed || force) SetFrame(frame);
        }

        private void ScheduleBlink(long now)
        {
            nextBlink = now + random.Next(2800, 6800);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                Interact();
                petMenu.ShowAt(Cursor.Position, settings.Height, settings.TopMost,
                    settings.UiScalePercent);
                return;
            }
            if (e.Button != MouseButtons.Left || !pressed) return;
            pressed = false;
            Capture = false;
            Interact();
            if (dragging)
            {
                dragging = false;
                recentClicks.Clear();
                poseUntil = 0;
                Location = new Point(baseX, baseY);
                SetPose(Pose.Idle);
                SaveSettings();
                ShowMessage("drag");
            }
            else
            {
                if (pose == Pose.Wake)
                {
                    ShowMessage("wake");
                }
                else
                {
                    HandleClick();
                }
            }
            KeepAtBottom();
        }

        private void HandleClick()
        {
            long now = clock.ElapsedMilliseconds;
            if (pose == Pose.Rapid)
            {
                // Refresh only the pose deadline, never the speech/cooldown.
                rapidPoseUntil = now + RapidReleaseMs;
                return;
            }
            recentClicks.Enqueue(now);
            while (recentClicks.Count > 0 && now - recentClicks.Peek() > 1800)
                recentClicks.Dequeue();

            if (recentClicks.Count >= 4)
            {
                recentClicks.Clear();
                motion.CancelRecoil();
                poseUntil = 0;
                rapidPoseUntil = now + RapidReleaseMs;
                SetPose(Pose.Rapid);
                if (now >= rapidCooldownUntil)
                {
                    rapidCooldownUntil = now + 20000;
                    specialUntil = now + BubbleDurationMs;
                    ShowMessage("rapid");
                }
                else if (now >= specialUntil)
                {
                    // A silent reaction during cooldown must not retain an
                    // unrelated normal-click sentence from the previous pose.
                    bubble.Hide();
                    bubbleUntil = 0;
                }
                return;
            }
            motion.StartRecoil(now);
            SetPose(ClickPoses[nextClickPose]);
            nextClickPose = (nextClickPose + 1) % ClickPoses.Length;
            poseUntil = now + 650;
            if (now >= specialUntil)
                ShowMessage("click");
        }

        private void Interact() { lastInteraction = clock.ElapsedMilliseconds; }

        private static Point PhysicalMousePosition()
        {
            NativeMethods.Point position;
            if (!NativeMethods.GetPhysicalCursorPos(out position))
                throw new System.ComponentModel.Win32Exception(
                    System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            return new Point(position.X, position.Y);
        }

        private void SetPose(Pose next)
        {
            if (pose == next) return;
            if (pose == Pose.Rapid)
            {
                rapidPoseUntil = 0;
                recentClicks.Clear();
            }
            if (next == Pose.Idle && pose != Pose.Blink) layeredIdle.Enter(clock.ElapsedMilliseconds);
            if (next == Pose.Sleep) sleepStarted = clock.ElapsedMilliseconds;
            if (next == Pose.Drag)
            {
                dragStarted = clock.ElapsedMilliseconds;
                dragAnimation.ResetMotion(PhysicalMousePosition());
            }
            pose = next;
            RenderMotion(clock.ElapsedMilliseconds, true);
        }

        internal void EnterSleep()
        {
            if (sleeping) return;
            sleeping = true;
            recentClicks.Clear();
            motion.CancelRecoil();
            bubble.Hide();
            bubbleUntil = 0;
            poseUntil = 0;
            SetPose(Pose.Sleep);
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            long now = clock.ElapsedMilliseconds;
            // MouseMove can be coalesced while a layered window is updating.
            // Sample the held pointer once per visual tick as a fallback.
            if (dragging && pressed) UpdateDragPosition(PhysicalMousePosition());
            if (!sleeping && !pressed && now - lastInteraction >= SleepAfterMs)
                EnterSleep();

            if (bubbleUntil > 0 && now >= bubbleUntil)
            {
                bubble.Hide();
                bubbleUntil = 0;
            }

            if (!sleeping && !dragging && !pressed)
            {
                if (pose == Pose.Rapid && now >= rapidPoseUntil)
                {
                    SetPose(Pose.Idle);
                    ScheduleBlink(now);
                }
                if (poseUntil > 0 && now >= poseUntil)
                {
                    poseUntil = 0;
                    SetPose(Pose.Idle);
                }
                if (pose == Pose.Idle && now >= nextBlink)
                {
                    blinkStarted = now;
                    blinkUntil = now + 45 + random.Next(110, 171) + 50;
                    SetPose(Pose.Blink);
                }
                if (pose == Pose.Blink && now >= blinkUntil)
                {
                    SetPose(Pose.Idle);
                    // An occasional second blink breaks up the regular pattern.
                    if (!doubleBlink && random.Next(10) == 0)
                    {
                        doubleBlink = true;
                        nextBlink = now + random.Next(150, 241);
                    }
                    else
                    {
                        doubleBlink = false;
                        ScheduleBlink(now);
                    }
                }
            }

            bool attentive = Visible && !pressed && !sleeping &&
                (pose == Pose.Idle || pose == Pose.Blink);
            Point pointer = attentive ? PhysicalMousePosition() : Point.Empty;
            gaze.Update(now, pointer, new Rectangle(Location, sprites.Get(Pose.Idle).Size), attentive);
            if (Visible) RenderMotion(now, false);
        }

        private void ChangeDialogue()
        {
            Interact();
            if (pose == Pose.Rapid) SetPose(Pose.Idle);
            if (sleeping)
            {
                sleeping = false;
                SetPose(Pose.Wake);
                poseUntil = clock.ElapsedMilliseconds + 850;
                ScheduleBlink(clock.ElapsedMilliseconds);
            }
            ShowMessage(pose == Pose.Wake ? "wake" : "click");
        }

        private void ShowMessage(string category)
        {
            bubbleText = category == "click" ? dialogues.PickClick(pose) : dialogues.Pick(category);
            bubbleUntil = clock.ElapsedMilliseconds + BubbleDurationMs;
            LayoutBubble();
            if (Visible && !bubble.Visible) bubble.Show();
            KeepAtBottom();
        }

        private void LayoutBubble()
        {
            Rectangle pet = new Rectangle(Location, sprites.Get(Pose.Idle).Size);
            Rectangle work = Screen.FromRectangle(pet).WorkingArea;
            int freeLeft = pet.Left - work.Left;
            int freeRight = work.Right - pet.Right;
            int freeAbove = pet.Top - work.Top;
            int freeBelow = work.Bottom - pet.Bottom;

            if (pet.Left + pet.Width / 2 >= work.Left + work.Width / 2 && freeLeft >= 302)
                bubblePlacement = BubblePlacement.Left;
            else if (freeRight >= 302)
                bubblePlacement = BubblePlacement.Right;
            else if (freeLeft >= 302)
                bubblePlacement = BubblePlacement.Left;
            else if (freeAbove >= 125)
                bubblePlacement = BubblePlacement.Above;
            else if (freeBelow >= 125)
                bubblePlacement = BubblePlacement.Below;
            else
                bubblePlacement = freeLeft >= freeRight ? BubblePlacement.Left : BubblePlacement.Right;

            bubble.Render(bubbleText, bubblePlacement);
            PositionBubble();
        }

        private void PositionBubble()
        {
            Rectangle pet = new Rectangle(Location, sprites.Get(Pose.Idle).Size);
            Rectangle work = Screen.FromRectangle(pet).WorkingArea;
            int x, y;
            if (bubblePlacement == BubblePlacement.Left)
            {
                x = pet.Left - bubble.Width + 7;
                y = pet.Top + 12;
            }
            else if (bubblePlacement == BubblePlacement.Right)
            {
                x = pet.Right - 7;
                y = pet.Top + 12;
            }
            else if (bubblePlacement == BubblePlacement.Above)
            {
                x = pet.Left + (pet.Width - bubble.Width) / 2;
                y = pet.Top - bubble.Height + 7;
            }
            else
            {
                x = pet.Left + (pet.Width - bubble.Width) / 2;
                y = pet.Bottom - 7;
            }
            x = Clamp(x, work.Left, work.Right - bubble.Width);
            y = Clamp(y, work.Top, work.Bottom - bubble.Height);
            bubble.Location = new Point(x, y);
        }

        private void ChangeSize(int height)
        {
            if (ApplySize(height)) SaveSettings();
        }

        private bool ApplySize(int height)
        {
            Interact();
            if (settings.Height == height) return false;
            settings.Height = height;
            motion.Reset();
            sleepAnimation.Reset();
            sprites.Resize(height);
            dragAnimation.Resize(sprites.Width, sprites.Height);
            layeredIdle.Resize(height);
            RenderMotion(clock.ElapsedMilliseconds, true);
            ClampToVisibleScreen();
            Location = new Point(baseX, baseY);
            if (bubble.Visible) LayoutBubble();
            KeepAtBottom();
            return true;
        }

        private void ClampToVisibleScreen()
        {
            Rectangle desired = new Rectangle(baseX, baseY, sprites.Width, sprites.Height);
            Rectangle work = Screen.FromRectangle(desired).WorkingArea;
            baseX = Clamp(baseX, work.Left, work.Right - sprites.Width);
            baseY = Clamp(baseY, work.Top, work.Bottom - sprites.Height);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum) return minimum;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { OnDisplaySettingsChanged(sender, e); });
                return;
            }
            ClampToVisibleScreen();
            Location = new Point(baseX, baseY);
            if (bubble.Visible) LayoutBubble();
            SaveSettings();
            KeepAtBottom();
        }

        private void SaveSettings()
        {
            settings.X = baseX;
            settings.Y = baseY;
            settings.HasPosition = true;
            settingsStore.Save(settings);
        }

        private void HideToTray()
        {
            Interact();
            recentClicks.Clear();
            if (pose == Pose.Rapid) SetPose(Pose.Idle);
            bubble.Hide();
            bubbleUntil = 0;
            Hide();
            SaveSettings();
        }

        private void RestoreFromTray()
        {
            Interact();
            ClampToVisibleScreen();
            Location = new Point(baseX, baseY);
            RenderMotion(clock.ElapsedMilliseconds, true);
            Show();
            ApplyWindowLayer();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            SaveSettings();
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            tray.Visible = false;
            tray.Dispose();
            trayIcon.Dispose();
            petMenu.Dispose();
            trayMenu.Dispose();
            bubble.Close();
            bubble.Dispose();
            timer.Dispose();
            motion.Dispose();
            sleepAnimation.Dispose();
            dragAnimation.Dispose();
            layeredIdle.Dispose();
            sprites.Dispose();
            base.OnFormClosed(e);
        }
    }
}
