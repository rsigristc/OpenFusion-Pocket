package com.openfusion.dualscreen;

/** Pure layout policy: tested on the host JVM, shared by the real Android bridge. */
public final class DualScreenLayout {
    public static final class Box {
        public final int left, top, right, bottom;
        public Box(int l, int t, int r, int b) { left = l; top = t; right = r; bottom = b; }
        public int width() { return right - left; }
        public int height() { return bottom - top; }
        boolean contains(Box b) { return b.left >= left && b.top >= top && b.right <= right && b.bottom <= bottom; }
    }
    public static final class Plan {
        public final Box primary, secondary;
        public final boolean usable;
        Plan(Box p, Box s, boolean ok) { primary = p; secondary = s; usable = ok; }
    }
    public static Plan fold(Box area, float density, boolean evidence, boolean flat, boolean separating, boolean horizontal, Box hinge) {
        Box p = area, s = area;
        if (separating && hinge != null) {
            if (horizontal) { p = new Box(area.left, area.top, area.right, hinge.top); s = new Box(area.left, hinge.bottom, area.right, area.bottom); }
            else { p = new Box(area.left, area.top, hinge.left, area.bottom); s = new Box(hinge.right, area.top, area.right, area.bottom); }
        } else if (flat) {
            int boundary = area.top + Math.max((int)Math.ceil(270 * density), Math.min((int)(area.width() * 9f / 16f), area.height() - (int)Math.ceil(300 * density)));
            p = new Box(area.left, area.top, area.right, boundary); s = new Box(area.left, boundary, area.right, area.bottom);
        }
        boolean ok = evidence && (flat || separating) && area.contains(p) && area.contains(s) &&
            (!separating || hinge != null && (horizontal ? hinge.left <= area.left && hinge.right >= area.right : hinge.top <= area.top && hinge.bottom >= area.bottom)) &&
            density > 0 && !Float.isNaN(density) && !Float.isInfinite(density) &&
            p.width() / density >= 480 && p.height() / density >= 270 && p.width() >= p.height() * 1.2f &&
            s.width() / density >= 320 && s.height() / density >= 300 &&
            (p.right <= s.left || p.bottom <= s.top);
        return new Plan(p, s, ok);
    }
}
