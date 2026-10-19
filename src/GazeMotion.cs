using System;
using System.Drawing;

namespace MakiseDesktopPet
{
    // Pointer position is sampled only while the pet is awake and visible.
    // Eyes respond first; the head follows with a smaller, slower movement.
    internal sealed class GazeMotion
    {
        private long lastUpdate = -1;
        internal double EyeX { get; private set; }
        internal double EyeY { get; private set; }
        internal double HeadX { get; private set; }
        internal double HeadY { get; private set; }

        private static double Target(double distance, double deadZone, double reach)
        {
            double remaining = Math.Max(0, Math.Abs(distance) - deadZone);
            return Math.Sign(distance) * Math.Tanh(remaining / reach);
        }

        private static double Approach(double current, double target, double elapsed, double responseMs)
        {
            return current + (target - current) * (1 - Math.Exp(-elapsed / responseMs));
        }

        internal void Update(long now, Point cursor, Rectangle petBounds, bool attentive)
        {
            double targetX = 0, targetY = 0;
            if (attentive)
            {
                double faceX = petBounds.Left + petBounds.Width * 0.51;
                double faceY = petBounds.Top + petBounds.Height * 0.33;
                targetX = Target(cursor.X - faceX, 9, 220);
                targetY = Target(cursor.Y - faceY, 9, 200);
            }

            if (lastUpdate < 0 || now < lastUpdate)
            {
                lastUpdate = now;
                return;
            }
            double elapsed = Math.Min(80, now - lastUpdate);
            lastUpdate = now;
            EyeX = Approach(EyeX, targetX, elapsed, 90);
            EyeY = Approach(EyeY, targetY, elapsed, 110);
            HeadX = Approach(HeadX, targetX, elapsed, 340);
            HeadY = Approach(HeadY, targetY, elapsed, 390);
        }
    }
}
