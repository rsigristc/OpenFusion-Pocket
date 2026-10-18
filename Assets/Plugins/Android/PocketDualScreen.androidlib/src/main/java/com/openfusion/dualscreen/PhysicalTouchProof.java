package com.openfusion.dualscreen;

/** Reuse an actual touch test only for the same display connection and live input device. */
final class PhysicalTouchProof {
    static boolean matches(int savedBoot, int boot, int savedDisplay, int display,
                           String savedSurface, String surface, String savedTouch, String[] touches) {
        if (boot < 0 || savedBoot != boot || display < 0 || savedDisplay != display ||
            savedSurface == null || savedSurface.isEmpty() || !savedSurface.equals(surface) ||
            savedTouch == null || savedTouch.isEmpty() || touches == null) return false;
        int count = 0;
        for (String touch : touches) if (savedTouch.equals(touch)) count++;
        return count == 1; // Ambiguous descriptors cannot prove which device received the test.
    }
}
