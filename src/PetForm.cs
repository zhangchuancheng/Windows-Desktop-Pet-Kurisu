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
        // Retain the original warp renderer for optional restoration.
        private static readonly bool EnableOriginalDragAnimation = false;
        private static readonly Pose[] ClickPoses =
            { Pose.PokeBlock, Pose.PokeFold, Pose.PokeCheek };
        private readonly string appDirectory;
        private readonly SettingsStore settingsStore;
        private readonly PetSettings settings;
        private SpriteAssets sprites;
        private readonly Dialogues dialogues;
        private readonly BubbleForm bubble;
        private readonly OllamaChatClient chatClient;
        private readonly ChatForm chat;
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly PetMenuPopup petMenu;
        private readonly ContextMenuStrip outfitMenu;
        private readonly ContextMenuStrip trayMenu;
        private readonly Timer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private readonly Queue<long> recentClicks = new Queue<long>();
        private readonly PetMotion motion = new PetMotion();
        private readonly SleepAnimation sleepAnimation = new SleepAnimation();
        private DragWarpAnimation dragAnimation;
        private IdleMotionAnimation idleAnimation;
        private readonly GazeMotion gaze = new GazeMotion();
        private string outfitId;

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
        private bool bubbleFromChat;
        private BubblePlacement bubblePlacement;
        private bool settingsSaveFailed;
        private bool closing;

        internal PetForm(string directory)
            : base(false)
        {
            appDirectory = directory;
            settingsStore = new SettingsStore();
            settings = settingsStore.Load();
            outfitId = OutfitCatalog.Normalize(settings.OutfitId);
            try
            {
                CreateOutfitResources(outfitId, settings.Height,
                    out sprites, out idleAnimation, out dragAnimation);
            }
            catch
            {
                if (outfitId == OutfitCatalog.OriginalId) throw;
                outfitId = OutfitCatalog.OriginalId;
                CreateOutfitResources(outfitId, settings.Height,
                    out sprites, out idleAnimation, out dragAnimation);
            }
            settings.OutfitId = outfitId;
            dialogues = new Dialogues(System.IO.Path.Combine(directory, "dialogues.json"));
            bubble = new BubbleForm();
            chatClient = new OllamaChatClient(directory);
            chat = new ChatForm(chatClient, ShowChatReply, InteractWithChat);
            TopMost = settings.TopMost;
            bubble.TopMost = settings.TopMost;
            chat.TopMost = settings.TopMost;

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
            idleAnimation.Enter(clock.ElapsedMilliseconds);
            RenderMotion(clock.ElapsedMilliseconds, true);

            petMenu = BuildPetMenu();
            outfitMenu = BuildOutfitMenu();
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
                ChangeUiScale, ChangeDialogue, OpenOutfitPicker, OpenChat, OpenChatPreferences,
                HideToTray, Close, KeepAtBottom);
        }

        private void CreateOutfitResources(string id, int height,
            out SpriteAssets nextSprites, out IdleMotionAnimation nextIdle,
            out DragWarpAnimation nextDrag)
        {
            nextSprites = null;
            nextIdle = null;
            nextDrag = null;
            string spriteDirectory = OutfitCatalog.SpriteDirectory(appDirectory, id);
            try
            {
                nextSprites = new SpriteAssets(spriteDirectory, height);
                nextIdle = id == OutfitCatalog.OriginalId
                    ? new IdleMotionAnimation(spriteDirectory, height)
                    : new IdleMotionAnimation(spriteDirectory, spriteDirectory, height, true, true);
                nextDrag = new DragWarpAnimation(spriteDirectory,
                    nextSprites.Width, nextSprites.Height,
                    id == OutfitCatalog.OriginalId && EnableOriginalDragAnimation);
            }
            catch
            {
                if (nextDrag != null) nextDrag.Dispose();
                if (nextIdle != null) nextIdle.Dispose();
                if (nextSprites != null) nextSprites.Dispose();
                nextDrag = null;
                nextIdle = null;
                nextSprites = null;
                throw;
            }
        }

        private ContextMenuStrip BuildOutfitMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
            {
                string selectedId = outfit.Id;
                ToolStripMenuItem item = new ToolStripMenuItem(outfit.Name);
                item.Tag = selectedId;
                item.Click += delegate { ChangeOutfit(selectedId); };
                menu.Items.Add(item);
            }
            menu.Opening += delegate
            {
                foreach (ToolStripMenuItem item in menu.Items)
                    item.Checked = (string)item.Tag == outfitId;
            };
            return menu;
        }

        private void OpenOutfitPicker()
        {
            Interact();
            Point pointer = Cursor.Position;
            // Let the settings popup finish its mouse-up and close sequence
            // before showing another dropdown from this no-activate window.
            BeginInvoke((MethodInvoker)delegate
            {
                if (IsDisposed || !Visible) return;
                NativeMethods.SetForegroundWindow(Handle);
                outfitMenu.Show(pointer);
                NativeMethods.SetWindowPos(outfitMenu.Handle, NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0, NativeMethods.SWP_NOMOVE |
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            });
        }

        private void ChangeOutfit(string selectedId)
        {
            selectedId = OutfitCatalog.Normalize(selectedId);
            if (selectedId == outfitId) return;
            SpriteAssets nextSprites;
            IdleMotionAnimation nextIdle;
            DragWarpAnimation nextDrag;
            try
            {
                CreateOutfitResources(selectedId, settings.Height,
                    out nextSprites, out nextIdle, out nextDrag);
            }
            catch (Exception error)
            {
                MessageBox.Show("服装素材加载失败，已保留当前服装。\n" + error.Message,
                    "切换服装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SpriteAssets previousSprites = sprites;
            IdleMotionAnimation previousIdle = idleAnimation;
            DragWarpAnimation previousDrag = dragAnimation;
            string previousId = outfitId;
            Pose previousPose = pose;
            bool wasSleeping = sleeping;
            try
            {
                sprites = nextSprites;
                idleAnimation = nextIdle;
                dragAnimation = nextDrag;
                outfitId = selectedId;
                settings.OutfitId = selectedId;
                pressed = dragging = sleeping = false;
                Capture = false;
                recentClicks.Clear();
                motion.Reset();
                sleepAnimation.Reset();
                poseUntil = blinkUntil = rapidPoseUntil = 0;
                pose = Pose.Idle;
                long now = clock.ElapsedMilliseconds;
                idleAnimation.Enter(now);
                ScheduleBlink(now);
                ClampToVisibleScreen();
                Location = new Point(baseX, baseY);
                RenderMotion(now, true);
                if (bubble.Visible)
                {
                    if (bubbleFromChat) LayoutChatBubble();
                    else LayoutBubble();
                }
                KeepAtBottom();
                SaveSettings();
            }
            catch (Exception error)
            {
                sprites = previousSprites;
                idleAnimation = previousIdle;
                dragAnimation = previousDrag;
                outfitId = previousId;
                settings.OutfitId = previousId;
                pose = previousPose;
                sleeping = wasSleeping;
                RenderMotion(clock.ElapsedMilliseconds, true);
                if (nextDrag != null) nextDrag.Dispose();
                nextIdle.Dispose();
                nextSprites.Dispose();
                MessageBox.Show("切换服装失败，已恢复当前服装。\n" + error.Message,
                    "切换服装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (previousDrag != null) previousDrag.Dispose();
            previousIdle.Dispose();
            previousSprites.Dispose();
            Interact();
        }

        private void ChangeUiScale(int percent)
        {
            if (settings.UiScalePercent == percent) return;
            settings.UiScalePercent = percent;
            chat.ApplyUiScale(percent);
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
            chat.TopMost = settings.TopMost;
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
            ToolStripMenuItem outfits = new ToolStripMenuItem("切换服装");
            outfits.Tag = "outfit-picker";
            foreach (OutfitCatalog.Outfit outfit in OutfitCatalog.All)
            {
                string selectedId = outfit.Id;
                ToolStripMenuItem item = new ToolStripMenuItem(outfit.Name);
                item.Tag = selectedId;
                item.Click += delegate { ChangeOutfit(selectedId); };
                outfits.DropDownItems.Add(item);
            }
            outfits.DropDownOpening += delegate
            {
                foreach (ToolStripMenuItem item in outfits.DropDownItems)
                    item.Checked = (string)item.Tag == outfitId;
            };
            menu.Items.Add(outfits);
            menu.Items.Add("聊两句", null, delegate { OpenChat(); });
            menu.Items.Add("聊天偏好", null, delegate { OpenChatPreferences(); });
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

        private void CancelPointerInteraction()
        {
            if (!pressed && !dragging) return;
            bool wasDragging = dragging;
            pressed = dragging = false;
            Capture = false;
            recentClicks.Clear();
            motion.CancelRecoil();
            if (wasDragging)
            {
                poseUntil = 0;
                SetPose(Pose.Idle);
            }
            Interact();
            ScheduleBlink(clock.ElapsedMilliseconds);
            if (wasDragging) SaveSettings();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            // Normal mouse-up clears pressed before releasing capture.
            if (!Capture && pressed) CancelPointerInteraction();
        }

        protected override void WndProc(ref Message message)
        {
            const int WM_CANCELMODE = 0x001F;
            if (message.Msg == WM_CANCELMODE) CancelPointerInteraction();
            base.WndProc(ref message);
        }

        private void UpdateDragPosition(Point pointer)
        {
            if (dragAnimation != null) dragAnimation.ObservePointer(pointer);
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
                if (dragAnimation == null)
                {
                    if (force) SetFrame(CurrentFrame());
                    return;
                }
                bool dragChanged;
                Bitmap dragFrame = dragAnimation.Render(now - dragStarted, out dragChanged);
                if (dragChanged || force) SetFrame(dragFrame);
                return;
            }
            if (pose == Pose.Idle || pose == Pose.Blink)
            {
                int eyes = pose == Pose.Blink ? IdleMotionAnimation.EyeForBlink(now, blinkStarted, blinkUntil) : 0;
                bool updated;
                Bitmap idleFrame = idleAnimation.Render(now, eyes,
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
                // The pet normally refuses activation. A user-opened menu needs
                // a foreground owner, otherwise ToolStrip can dismiss it on
                // AppFocusChange before the selection receives its mouse-up.
                NativeMethods.SetForegroundWindow(Handle);
                petMenu.ShowAt(Cursor.Position, settings.Height, settings.TopMost,
                    settings.UiScalePercent, OutfitCatalog.DisplayName(outfitId));
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
            if (next == Pose.Idle && pose != Pose.Blink) idleAnimation.Enter(clock.ElapsedMilliseconds);
            if (next == Pose.Sleep) sleepStarted = clock.ElapsedMilliseconds;
            if (next == Pose.Drag)
            {
                dragStarted = clock.ElapsedMilliseconds;
                if (dragAnimation != null)
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

        private void InteractWithChat()
        {
            Interact();
            if (!sleeping) return;
            sleeping = false;
            SetPose(Pose.Wake);
            poseUntil = clock.ElapsedMilliseconds + 850;
            ScheduleBlink(clock.ElapsedMilliseconds);
        }

        internal void OpenChat()
        {
            if (!Visible) RestoreFromTray();
            InteractWithChat();
            chat.ShowNear(new Rectangle(Location, sprites.Get(Pose.Idle).Size),
                settings.TopMost, settings.UiScalePercent);
        }

        private void OpenChatPreferences()
        {
            if (!Visible) RestoreFromTray();
            InteractWithChat();
            chat.HideChat();
            try
            {
                using (ChatPreferencesForm editor = new ChatPreferencesForm(
                    chatClient.GetPreferences(), settings.UiScalePercent,
                    Screen.FromRectangle(Bounds).WorkingArea.Size))
                {
                    editor.TopMost = settings.TopMost;
                    if (editor.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        chatClient.SavePreferences(editor.Value);
                        ShowChatReply("偏好记下了。接下来按新设定聊。");
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(this, "保存聊天偏好失败：" + error.Message,
                            "桌宠设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                KeepAtBottom();
            }
        }

        private void ShowChatReply(string answer)
        {
            InteractWithChat();
            bubbleText = answer;
            bubbleFromChat = true;
            bubbleUntil = clock.ElapsedMilliseconds + Math.Min(18000,
                Math.Max(8000, answer.Length * 160));
            LayoutChatBubble();
            if (Visible && !bubble.Visible) bubble.Show();
            KeepAtBottom();
        }

        private void ShowMessage(string category)
        {
            bubbleText = category == "click" ? dialogues.PickClick(pose) : dialogues.Pick(category);
            bubbleFromChat = false;
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

        private void LayoutChatBubble()
        {
            if (!chat.Visible)
            {
                LayoutBubble();
                return;
            }
            Rectangle pet = new Rectangle(Location, sprites.Get(Pose.Idle).Size);
            Rectangle work = Screen.FromRectangle(pet).WorkingArea;
            bubble.Render(bubbleText, BubblePlacement.Below);
            if (pet.Bottom - 7 + bubble.Height <= work.Bottom)
            {
                bubblePlacement = BubblePlacement.Below;
                PositionBubble();
                return;
            }
            bubble.Render(bubbleText, BubblePlacement.Above);
            if (pet.Top - bubble.Height + 7 >= work.Top)
            {
                bubblePlacement = BubblePlacement.Above;
                PositionBubble();
                return;
            }
            LayoutBubble();
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
            if (dragAnimation != null) dragAnimation.Resize(sprites.Width, sprites.Height);
            idleAnimation.Resize(height);
            RenderMotion(clock.ElapsedMilliseconds, true);
            ClampToVisibleScreen();
            Location = new Point(baseX, baseY);
            chat.KeepVisible();
            if (bubble.Visible)
            {
                if (bubbleFromChat) LayoutChatBubble();
                else LayoutBubble();
            }
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
            chat.KeepVisible();
            if (bubble.Visible)
            {
                if (bubbleFromChat) LayoutChatBubble();
                else LayoutBubble();
            }
            SaveSettings();
            KeepAtBottom();
        }

        private void SaveSettings()
        {
            settings.X = baseX;
            settings.Y = baseY;
            settings.HasPosition = true;
            try
            {
                settingsStore.Save(settings);
                settingsSaveFailed = false;
            }
            catch (System.IO.IOException error) { ReportSettingsSaveFailure(error); }
            catch (UnauthorizedAccessException error) { ReportSettingsSaveFailure(error); }
            catch (System.Security.SecurityException error) { ReportSettingsSaveFailure(error); }
        }

        private void ReportSettingsSaveFailure(Exception error)
        {
            Trace.TraceError("Unable to save desktop pet settings: " + error);
            if (!settingsSaveFailed && !closing && tray != null && tray.Visible)
                tray.ShowBalloonTip(5000, "设置暂未保存",
                    "当前设置仍然有效，但暂时无法写入文件。请检查文件占用或权限；下次调整时会重试。",
                    ToolTipIcon.Warning);
            settingsSaveFailed = true;
        }

        private void HideToTray()
        {
            CancelPointerInteraction();
            Interact();
            chat.HideChat();
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
            closing = true;
            pressed = dragging = false;
            Capture = false;
            timer.Stop();
            try { SaveSettings(); }
            finally { ReleaseResources(e); }
        }

        private void ReleaseResources(FormClosedEventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            tray.Visible = false;
            tray.Dispose();
            trayIcon.Dispose();
            petMenu.Dispose();
            outfitMenu.Dispose();
            trayMenu.Dispose();
            chat.Dispose();
            chatClient.Dispose();
            bubble.Close();
            bubble.Dispose();
            timer.Dispose();
            motion.Dispose();
            sleepAnimation.Dispose();
            if (dragAnimation != null) dragAnimation.Dispose();
            idleAnimation.Dispose();
            sprites.Dispose();
            base.OnFormClosed(e);
        }
    }
}
