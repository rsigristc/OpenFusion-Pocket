package com.openfusion.dualscreen;

import android.content.Context;
import android.view.MotionEvent;
import android.widget.HorizontalScrollView;

/** Settles on complete 44dp filter tiles; the containing viewport is a whole tile count. */
final class MapFilterScroll extends HorizontalScrollView {
    final int tileWidth;
    private boolean touching;
    private final Runnable snap=this::snapToTile;
    private void snapToTile() {
        if(touching || getChildCount()==0 || getWidth()==0) return;
        int max=Math.max(0,getChildAt(0).getWidth()-getWidth());
        int target=Math.min(max,Math.max(0,Math.round(getScrollX()/(float)tileWidth)*tileWidth));
        if(target!=getScrollX()) scrollTo(target,0);
    }
    MapFilterScroll(Context c) {
        super(c); tileWidth=Math.round(44*c.getResources().getDisplayMetrics().density);
        setHorizontalScrollBarEnabled(true); setHorizontalFadingEdgeEnabled(false);
        setScrollbarFadingEnabled(false); setScrollBarStyle(android.view.View.SCROLLBARS_INSIDE_INSET);
        setContentDescription("Map filters. Swipe horizontally for more categories.");
    }
    private void settle() { removeCallbacks(snap); postDelayed(snap,160); }
    protected void onScrollChanged(int x,int y,int oldX,int oldY) { super.onScrollChanged(x,y,oldX,oldY); settle(); }
    public boolean dispatchTouchEvent(MotionEvent event) {
        int action=event.getActionMasked();
        if(action==MotionEvent.ACTION_DOWN) { touching=true; removeCallbacks(snap); }
        boolean handled=super.dispatchTouchEvent(event);
        if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_CANCEL) { touching=false; settle(); }
        return handled;
    }
    protected void onSizeChanged(int w,int h,int oldW,int oldH) { super.onSizeChanged(w,h,oldW,oldH); settle(); }
    protected void onDetachedFromWindow() { removeCallbacks(snap); super.onDetachedFromWindow(); }
}
