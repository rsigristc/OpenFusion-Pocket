package com.openfusion.dualscreen;

import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.StateListDrawable;
import android.view.View;
import android.widget.Button;

/** Lightweight game-style chrome; no extra textures, cameras or per-frame allocations. */
final class PanelSkin {
    static final int CYAN = Color.rgb(50,235,255), LIME = Color.rgb(220,255,0);
    static GradientDrawable plate(View v, boolean selected) {
        float d=v.getResources().getDisplayMetrics().density;
        GradientDrawable g=new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM,
            selected ? new int[]{Color.rgb(226,255,45),Color.rgb(133,187,0)} :
                       new int[]{Color.rgb(8,65,89),Color.rgb(0,19,34)});
        g.setCornerRadius(5*d); g.setStroke(Math.max(1,Math.round(d)),selected ? LIME : CYAN);
        return g;
    }
    static void cell(View v, boolean selected) { v.setBackground(plate(v,selected)); }
    static void button(Button b) {
        StateListDrawable bg=new StateListDrawable();
        GradientDrawable disabled=plate(b,false); disabled.setAlpha(90);
        bg.addState(new int[]{-android.R.attr.state_enabled},disabled);
        bg.addState(new int[]{android.R.attr.state_pressed},plate(b,true));
        bg.addState(new int[]{android.R.attr.state_selected},plate(b,true));
        bg.addState(new int[]{},plate(b,false)); b.setBackground(bg); b.setBackgroundTintList(null);
        b.setTextColor(new ColorStateList(new int[][]{{-android.R.attr.state_enabled},{android.R.attr.state_pressed},{android.R.attr.state_selected},{}},
            new int[]{Color.rgb(102,132,144),Color.rgb(0,23,32),Color.rgb(0,23,32),CYAN}));
        b.setTypeface(Typeface.DEFAULT,Typeface.BOLD); b.setAllCaps(false);
        b.setElevation(0); b.setStateListAnimator(null);
    }
}
