package com.openfusion.dualscreen;

public final class DualScreenLayoutChecks {
    static void check(boolean ok, String message) { if (!ok) throw new AssertionError(message); }
    static DualScreenLayout.Box b(int l, int t, int r, int bottom) { return new DualScreenLayout.Box(l, t, r, bottom); }
    public static void main(String[] args) {
        DualScreenLayout.Box screen = b(0, 0, 1800, 1600);
        DualScreenLayout.Plan p = DualScreenLayout.fold(screen, 2, true, true, false, false, null);
        check(p.usable && p.primary.bottom == p.secondary.top && p.primary.bottom == 1000, "Flat unfolded fold must yield non-overlapping usable panes");
        check(!DualScreenLayout.fold(screen, 2, false, true, false, false, null).usable, "A large ordinary screen must never qualify");
        check(!DualScreenLayout.fold(screen, 2, true, false, false, false, null).usable, "Unknown posture must fail closed");
        check(!DualScreenLayout.fold(b(0, 0, 1080, 1920), 3, true, true, false, false, null).usable, "A narrow cover/flip-sized surface is not usable");
        p = DualScreenLayout.fold(screen, 2, true, false, true, true, b(0, 780, 1800, 820));
        check(p.usable && p.primary.bottom == 780 && p.secondary.top == 820, "Horizontal tabletop panes must avoid the occluded hinge");
        check(!DualScreenLayout.fold(screen, 2, true, false, true, false, b(880, 0, 920, 1600)).usable, "Narrow book panes must not break the legacy HUD");
        check(!DualScreenLayout.fold(screen, 2, true, false, true, true, b(0, -50, 1800, 20)).usable, "An off-window hinge must be rejected");
        check(!DualScreenLayout.fold(screen, 2, true, false, true, true, b(100, 780, 300, 820)).usable, "A partial feature cannot prove two usable panes");
        check(!DualScreenLayout.fold(b(0, 0, 1800, 800), 2, true, true, false, false, null).usable, "Multiwindow with insufficient height must fall back");
        p = DualScreenLayout.fold(b(24, 36, 1776, 1564), 2, true, true, false, false, null);
        check(p.usable && p.primary.left == 24 && p.primary.top == 36 && p.secondary.bottom == 1564, "Safe insets must be preserved");
        check(!DualScreenLayout.fold(screen, Float.NaN, true, true, false, false, null).usable, "Invalid density must be rejected");
        p = DualScreenLayout.fold(b(0, 141, 2448, 1848), 2.625f, true, true, false, false, null);
        check(p.usable && p.secondary.height() >= 788, "Fold hardware's fractional density must not round the minimum landscape pane BELOW 300 dp");
        p = DualScreenLayout.fold(b(0, 141, 1848, 2448), 2.625f, true, true, false, false, null);
        check(p.usable, "The same real Fold must remain usable in portrait");
        for (int i = 0; i < 100; i++) {
            boolean feature = i % 2 == 0;
            check(DualScreenLayout.fold(screen, 2, feature, true, false, false, null).usable == feature, "Fold/unfold must not latch a prior result");
        }
        System.out.println("PASS: actual native fold geometry — flat, unknown, conventional, cover, tabletop, book, off-window hinge, multiwindow, safe areas, density and 100 transitions");
    }
}
