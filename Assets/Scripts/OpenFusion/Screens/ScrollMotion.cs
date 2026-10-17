using UnityEngine;

namespace OpenFusion.Screens
{
    public class ScrollMotion
    {
        public float y, vel, maxY, lastMove; public bool down, dragging;
        public Vector2 start; public float startY;
        Vector2 previous; float previousTime;
        public void Bounds(float content, float viewport) { maxY = Mathf.Max(0, content - viewport); y = Mathf.Clamp(y, 0, maxY); }
        public void Begin(Vector2 position, float now) { down = true; dragging = false; start = previous = position; startY = y; vel = 0; previousTime = now; }
        public void Drag(Vector2 position, float now)
        {
            if (!down) return;
            float delta = position.y - start.y;
            if (!dragging && Mathf.Abs(delta) > 10f) dragging = true;
            if (dragging)
            {
                y = Mathf.Clamp(startY - delta, 0, maxY);
                if (Mathf.Abs(position.y - previous.y) > .01f)
                { vel = Mathf.Clamp((previous.y - position.y) / Mathf.Max(now - previousTime, .008f), -2000, 2000); lastMove = now; }
            }
            previous = position; previousTime = now;
        }
        public void End(float now) { down = false; if (now - lastMove > .08f) vel = 0; }
        public void Tick(float delta)
        {
            if (down) return;
            y = Mathf.Clamp(y + vel * delta, 0, maxY); vel *= Mathf.Exp(-10f * delta);
            if (Mathf.Abs(vel) < 1f || y == 0 || y == maxY) vel = 0;
        }
    }
}
