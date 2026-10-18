package com.openfusion.dualscreen;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.util.LruCache;
import android.view.MotionEvent;
import android.view.ScaleGestureDetector;
import android.view.View;
import android.widget.Button;
import android.widget.HorizontalScrollView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.FrameLayout;
import org.json.JSONArray;
import org.json.JSONObject;
import java.util.HashSet;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Presentation only: all coordinates, filters, equipment and energy come from the existing
 * Unity world/map/HUD. Commands return to its existing methods on Unity's main thread. */
final class DualPanel extends LinearLayout {
    private final BoostStrip boosts;
    private final MapLocation location;
    private final LinearLayout slotsRow;
    private final Button[] nanos = new Button[3];
    private final MapZoomButton zoomIn, zoomOut;
    private final MapTab localTab, worldTab;
    private final MapZoomTicks zoomTicks;
    private final LinearLayout filterRow;
    private final java.util.ArrayList<MapFilterTile> filters=new java.util.ArrayList<>();
    private final MapView map;
    private final NanoPane nanoPane;
    private final EquipmentPane equipmentPane;
    private final MissionPane missionPane;
    private Bitmap avatar;
    private long avatarAt;
    private final LinearLayout mapPage;
    private final NavigationTile[] navigation = new NavigationTile[6];
    private JSONObject data = new JSONObject();
    private final float density;
    private final Bitmap[] previewBuffers=new Bitmap[2];
    private int previewIndex, previewCount;
    private int[] previewIds;
    private long previewAt, markerLabelUntil;
    private final android.graphics.Rect previewSource=new android.graphics.Rect();
    DualPanel(Context context) {
        super(context); density = context.getResources().getDisplayMetrics().density;
        setOrientation(VERTICAL); setBackgroundColor(Color.rgb(0, 20, 32));
        // Secondary UI is touch-driven. Keep keyboard/gamepad focus on Unity gameplay.
        setDescendantFocusability(FOCUS_BLOCK_DESCENDANTS); setFocusable(false);
        setPadding(dp(8), dp(4), dp(8), dp(4));
        boosts = new BoostStrip(context,path -> panelImage(path)); addView(boosts, new LayoutParams(-1, dp(44)));
        LinearLayout slots = row(); slotsRow=slots;
        for (int i = 0; i < 3; i++) { final int slot = i; nanos[i] = button(slots, "Empty", () -> PocketDualScreen.send("nano", Integer.toString(slot), 0, 0)); }
        FrameLayout pages = new FrameLayout(context); addView(pages,new LayoutParams(-1,0,1));
        mapPage = new MapPage(context); mapPage.setOrientation(VERTICAL); pages.addView(mapPage,new FrameLayout.LayoutParams(-1,-1));
        LinearLayout tools = new LinearLayout(context); mapPage.addView(tools,new LayoutParams(-1,dp(48)));
        LinearLayout tabs=new LinearLayout(context); tools.addView(tabs,new LayoutParams(dp(170),-1));
        localTab=new MapTab(context,"LOCAL"); worldTab=new MapTab(context,"WORLD");
        tabs.addView(localTab,new LayoutParams(0,-1,1)); tabs.addView(worldTab,new LayoutParams(0,-1,1));
        localTab.setOnClickListener(v -> send("local","")); worldTab.setOnClickListener(v -> send("world",""));
        TextView filterLabel=text(10); filterLabel.setText(" FILTERS:"); tools.addView(filterLabel,new LayoutParams(dp(55),-1));
        MapFilterScroll filterScroll=new MapFilterScroll(context);
        filterRow=new LinearLayout(context); filterScroll.addView(filterRow,new FrameLayout.LayoutParams(-2,-1));
        FrameLayout filterViewport=new FrameLayout(context); tools.addView(filterViewport,new LayoutParams(0,-1,1));
        filterViewport.addView(filterScroll,new FrameLayout.LayoutParams(-1,-1));
        filterViewport.addOnLayoutChangeListener((v,l,t,r,b,oldL,oldT,oldR,oldB) -> {
            int available=r-l; if(available<=0) return;
            int width=available<filterScroll.tileWidth?available:available/filterScroll.tileWidth*filterScroll.tileWidth;
            android.view.ViewGroup.LayoutParams lp=filterScroll.getLayoutParams();
            if(lp.width!=width) { lp.width=width; filterScroll.setLayoutParams(lp); }
        });
        FrameLayout viewport=new FrameLayout(context); mapPage.addView(viewport,new LayoutParams(-1,0,1));
        map=new MapView(context); viewport.addView(map,new FrameLayout.LayoutParams(-1,-1));
        LinearLayout rail=new LinearLayout(context); rail.setOrientation(VERTICAL);
        FrameLayout.LayoutParams railParams=new FrameLayout.LayoutParams(dp(48),dp(132),android.view.Gravity.LEFT|android.view.Gravity.TOP);
        railParams.setMargins(dp(5),dp(8),0,0); viewport.addView(rail,railParams);
        zoomIn=new MapZoomButton(context,true); zoomOut=new MapZoomButton(context,false); zoomTicks=new MapZoomTicks(context);
        rail.addView(zoomIn,new LayoutParams(-1,dp(48))); rail.addView(zoomTicks,new LayoutParams(-1,dp(28))); rail.addView(zoomOut,new LayoutParams(-1,dp(48)));
        zoomIn.setOnClickListener(v -> send("zoom","in")); zoomOut.setOnClickListener(v -> send("zoom","out"));
        location=new MapLocation(context); mapPage.addView(location,new LayoutParams(-1,dp(27)));
        nanoPane = new NanoPane(context,path -> map.image(path),(canvas,id,destination,paint) -> drawNanoPreview(canvas,id,destination,paint)); pages.addView(nanoPane,new FrameLayout.LayoutParams(-1,-1)); nanoPane.setVisibility(GONE);
        equipmentPane=new EquipmentPane(context,path -> map.image(path),(canvas,destination,paint) -> {
            if(avatar==null || android.os.SystemClock.uptimeMillis()-avatarAt>2000) return false;
            float scale=Math.min(destination.width()/256,destination.height()/320),w=256*scale,h=320*scale;
            paint.setColor(Color.WHITE);paint.setAlpha(255);
            canvas.drawBitmap(avatar,null,new RectF(destination.centerX()-w/2,destination.centerY()-h/2,destination.centerX()+w/2,destination.centerY()+h/2),paint); return true;
        });
        pages.addView(equipmentPane,new FrameLayout.LayoutParams(-1,-1));equipmentPane.setVisibility(GONE);
        missionPane=new MissionPane(context,path -> map.image(path));pages.addView(missionPane,new FrameLayout.LayoutParams(-1,-1));missionPane.setVisibility(GONE);
        HorizontalScrollView scroll = new HorizontalScrollView(context); scroll.setFillViewport(true);
        LinearLayout nav = new LinearLayout(context); scroll.addView(nav,new FrameLayout.LayoutParams(-1,-1)); addView(scroll, new LayoutParams(-1, dp(50)));
        String[] labels = { "Map", "Nanos", "Equipment", "Missions", "Chat ↗", "E-Mail ↗" };
        String[] windows = { "Map", "Nanos", "Equipment", "Journal", "Chat", "EMail" };
        for (int i = 0; i < labels.length; i++) {
            final String window = windows[i]; NavigationTile b = new NavigationTile(context,labels[i],i);
            b.setContentDescription(labels[i].replace(" ↗", "") + ". Open the existing full-screen game panel.");
            navigation[i]=b;
            nav.addView(b, new LayoutParams(0, -1,1));
            if(i<4) { b.setContentDescription(window+" secondary panel"); b.setOnClickListener(v -> { showTab(window); send("tab",window); }); }
            else b.setOnClickListener(v -> send("window", window));
        }
    }
    Bitmap panelImage(String path) { return map==null?null:map.image(path); }
    private int dp(int value) { return Math.round(value * density); }
    private TextView text(int size) { TextView t = new TextView(getContext()); t.setTextColor(Color.CYAN); t.setTextSize(size); t.setGravity(android.view.Gravity.CENTER_VERTICAL); return t; }
    private LinearLayout row() { LinearLayout row = new LinearLayout(getContext()); addView(row, new LayoutParams(-1, dp(44))); return row; }
    private Button button(LinearLayout row, String label, Runnable action) {
        Button b = new Button(getContext()); b.setText(label); b.setTextSize(11); b.setTextColor(Color.CYAN);
        b.setPadding(dp(2), 0, dp(2), 0); b.setMinimumWidth(0); b.setMinWidth(0);
        PanelSkin.button(b); LayoutParams lp=new LayoutParams(0,-1,1); lp.setMargins(dp(2),dp(2),dp(2),dp(2));
        row.addView(b, lp); b.setOnClickListener(v -> action.run()); return b;
    }
    private void send(String action, String value) { PocketDualScreen.send(action, value, 0, 0); }
    private void showTab(String tab) {
        boolean collection="Nanos".equals(tab), equipment="Equipment".equals(tab), missions="Journal".equals(tab); nanoPane.setVisibility(collection ? VISIBLE : GONE); equipmentPane.setVisibility(equipment ? VISIBLE : GONE); missionPane.setVisibility(missions?VISIBLE:GONE); mapPage.setVisibility(collection || equipment || missions ? GONE : VISIBLE);
        slotsRow.setVisibility(GONE); // Equipped cards remain accessible on the Nanos tab.
        navigation[0].setSelected(!collection&&!equipment&&!missions);navigation[3].setSelected(missions); navigation[1].setSelected(collection); navigation[2].setSelected(equipment);
    }
    void update(String json) {
        try {
            data = new JSONObject(json);
            boosts.update(data);
            JSONArray slots = data.optJSONArray("nanos");
            for (int i = 0; i < 3; i++) {
                JSONObject n = slots == null ? null : slots.optJSONObject(i);
                nanos[i].setEnabled(data.optBoolean("canSummon") && !data.optBoolean("station") && n != null && n.optInt("id") > 0);
                nanos[i].setText(n == null ? "…" : (i + 1) + "  " + n.optString("name") + "  " + n.optInt("energy") + "/" + n.optInt("max") +
                    (n.optDouble("cooldown") > 0 ? " • CD "+String.format(java.util.Locale.US,"%.1fs",n.optDouble("cooldown")) : ""));
                nanos[i].setSelected(n != null && n.optBoolean("active"));
            }
            localTab.setSelected(data.optBoolean("local")); worldTab.setSelected(!data.optBoolean("local"));
            JSONArray filterData=data.optJSONArray("filters");
            if(filterData!=null) {
                while(filters.size()<filterData.length()) {
                    MapFilterTile tile=new MapFilterTile(getContext(),path -> map.image(path)); filters.add(tile);
                    filterRow.addView(tile,new LayoutParams(dp(44),-1));
                    tile.setOnClickListener(v -> send("filter",Integer.toString(tile.id)));
                    tile.setOnLongClickListener(v -> { android.widget.Toast.makeText(getContext(),tile.getContentDescription(),android.widget.Toast.LENGTH_SHORT).show(); return true; });
                }
                for(int i=0;i<filters.size();i++) {
                    JSONObject f=filterData.optJSONObject(i); filters.get(i).setVisibility(f==null?GONE:VISIBLE);
                    if(f!=null) filters.get(i).update(f,data.optBoolean("filtersEnabled"));
                }
            }
            zoomIn.setEnabled(data.optBoolean("zoomIn")); zoomOut.setEnabled(data.optBoolean("zoomOut"));
            zoomTicks.update(data.optBoolean("zoomIn"),data.optBoolean("zoomOut"));
            if(android.os.SystemClock.uptimeMillis()>=markerLabelUntil) location.setLocation(data.optString("location"));
            showTab(data.optString("tab","Map"));
            nanoPane.update(data.optJSONObject("collection"),data.optBoolean("connected"),data.optBoolean("canSummonCards"));
            missionPane.update(data.optJSONObject("missionsPanel"),data.optBoolean("connected"));
            equipmentPane.update(data.optJSONObject("equipment"),data.optBoolean("connected"),data.optInt("equipmentScroll"));
            for(int i=2;i<navigation.length;i++) navigation[i].setEnabled(!data.optBoolean("station")&&!data.optBoolean("dead"));
            map.invalidate();
        } catch (Exception e) { android.util.Log.e("PocketDS","Secondary panel update failed",e); location.setText("Waiting for live panel data…"); }
    }
    void avatarFrame(int[] pixels) {
        if(avatar==null)avatar=Bitmap.createBitmap(256,320,Bitmap.Config.ARGB_8888);
        avatar.setPixels(pixels,0,256,0,0,256,320);avatarAt=android.os.SystemClock.uptimeMillis();equipmentPane.invalidate();
    }
    void nanoFrame(int[] ids,int[] pixels) {
        int next=1-previewIndex;
        if(previewBuffers[next]==null) previewBuffers[next]=Bitmap.createBitmap(512,128,Bitmap.Config.ARGB_8888);
        previewBuffers[next].setPixels(pixels,0,512,0,0,512,128);
        previewIds=ids; previewIndex=next; previewAt=android.os.SystemClock.uptimeMillis();
        nanoPane.invalidatePreviews();
        previewCount++; if(previewCount==1 || previewCount%120==0) android.util.Log.i("PocketDS","Nano preview frames="+previewCount+" ids="+java.util.Arrays.toString(ids));
    }
    private boolean drawNanoPreview(Canvas canvas,int id,RectF destination,Paint paint) {
        Bitmap frame=previewBuffers[previewIndex];
        if(id<=0 || frame==null || previewIds==null || android.os.SystemClock.uptimeMillis()-previewAt>1500) return false;
        for(int i=0;i<previewIds.length;i++) if(previewIds[i]==id) {
            previewSource.set(i*128,0,(i+1)*128,128); canvas.drawBitmap(frame,previewSource,destination,paint); return true;
        }
        return false;
    }
    void release() { missionPane.release(); avatar=null; previewIds=null; previewBuffers[0]=previewBuffers[1]=null; nanoPane.release(); map.release(); }

    private final class MapView extends View {
        final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
        final android.graphics.Path border=new android.graphics.Path();
        final ExecutorService decoder = Executors.newSingleThreadExecutor();
        final HashSet<String> pending = new HashSet<>();
        final LruCache<String, Bitmap> images = new LruCache<String, Bitmap>(6 * 1024 * 1024) {
            protected int sizeOf(String key, Bitmap value) { return value.getByteCount(); }
            protected void entryRemoved(boolean evicted, String key, Bitmap old, Bitmap replacement) { if (old != replacement) old.recycle(); }
        };
        boolean released, dragging, scaled;
        float lastX, lastY, startX, startY, scale = 1;
        final ScaleGestureDetector pinch;
        MapView(Context context) {
            super(context); setContentDescription("Live world map. Drag to pan; pinch or use plus and minus to zoom. Tap a marker to read its name.");
            pinch = new ScaleGestureDetector(context, new ScaleGestureDetector.SimpleOnScaleGestureListener() {
                public boolean onScale(ScaleGestureDetector detector) {
                    scaled = true; scale *= detector.getScaleFactor();
                    if (scale > 1.25f || scale < .8f) { send("zoom", scale > 1 ? "in" : "out"); scale = 1; }
                    return true;
                }
            });
        }
        Bitmap image(String path) {
            if (path == null || path.isEmpty() || released) return null;
            Bitmap ready = images.get(path); if (ready != null) return ready;
            if (pending.add(path)) decoder.execute(() -> {
                Bitmap bitmap = BitmapFactory.decodeFile(path);
                post(() -> { pending.remove(path); if (released) { if (bitmap != null) bitmap.recycle(); return; } if (bitmap != null) images.put(path, bitmap); invalidate(); nanoPane.invalidateImages(); equipmentPane.invalidate(); missionPane.invalidateImages(); boosts.invalidate(); for(MapFilterTile tile:filters) tile.invalidate(); });
            });
            return null;
        }
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas); canvas.drawColor(Color.rgb(0, 12, 22));
            float w = getWidth(), h = getHeight(); if (w <= 0 || h <= 0) return;
            String notice = data.optString("notice");
            if (!notice.isEmpty()) { paint.setColor(Color.WHITE); paint.setTextSize(dp(13)); canvas.drawText("Map unavailable — see location / main screen", dp(10), h / 2, paint); return; }
            float x = (float)data.optDouble("x", 0), y = (float)data.optDouble("y", 0);
            float rw = (float)data.optDouble("width", 1), rh = (float)data.optDouble("height", 1);
            if (rw <= 0 || rh <= 0) return;
            Bitmap background = image(data.optString("image"));
            if (background != null) {
                // Shared Unity map UV is bottom-left; Android bitmaps are top-left.
                float sx = background.getWidth() / rw, sy = background.getHeight() / rh;
                canvas.save(); canvas.clipRect(0, 0, w, h);
                RectF dst = new RectF(-x * sx / background.getWidth() * w, -(1 - y - rh) * sy / background.getHeight() * h,
                    (1 - x) * sx / background.getWidth() * w, (y + rh) * sy / background.getHeight() * h);
                canvas.drawBitmap(background, null, dst, paint); canvas.restore();
            } else { paint.setColor(Color.CYAN); paint.setTextSize(dp(13)); canvas.drawText("Loading map texture…", dp(10), h / 2, paint); }
            // Subtle reference-style grid; markers and map geometry remain live Unity data.
            paint.setColor(Color.argb(24,55,224,249)); paint.setStrokeWidth(1);
            for(float gx=0;gx<w;gx+=dp(30)) canvas.drawLine(gx,0,gx,h,paint);
            for(float gy=0;gy<h;gy+=dp(30)) canvas.drawLine(0,gy,w,gy,paint);
            paint.setColor(Color.WHITE);
            JSONArray markers = data.optJSONArray("markers");
            if (markers != null) for (int i = 0; i < markers.length(); i++) {
                JSONObject mark = markers.optJSONObject(i); if (mark == null) continue;
                float px = ((float)mark.optDouble("x") - x) / rw * w, py = (1 - ((float)mark.optDouble("y") - y) / rh) * h;
                boolean clamp = mark.optInt("kind") == 1;
                if (clamp) { px = Math.max(dp(10), Math.min(w - dp(10), px)); py = Math.max(dp(10), Math.min(h - dp(10), py)); }
                else if (px < 0 || py < 0 || px > w || py > h) continue;
                Bitmap icon = image(mark.optString("image"));
                if (icon != null) {
                    canvas.save(); if (mark.optInt("kind") == 2) canvas.rotate((float)mark.optDouble("yaw"), px, py);
                    canvas.drawBitmap(icon, null, new RectF(px - dp(9), py - dp(9), px + dp(9), py + dp(9)), paint); canvas.restore();
                } else { paint.setColor(mark.optInt("kind") == 2 ? Color.CYAN : mark.optInt("kind") == 1 ? Color.YELLOW : Color.WHITE); canvas.drawCircle(px, py, dp(4), paint); }
            }
            paint.setColor(PocketArt.CYAN); paint.setStrokeWidth(dp(1)); paint.setStyle(Paint.Style.STROKE);
            float cut=dp(7); border.reset(); border.moveTo(cut,1); border.lineTo(w-cut,1); border.lineTo(w-1,cut);
            border.lineTo(w-1,h-cut); border.lineTo(w-cut,h-1); border.lineTo(cut,h-1); border.lineTo(1,h-cut); border.lineTo(1,cut); border.close();
            canvas.drawPath(border,paint); paint.setStyle(Paint.Style.FILL);
        }
        public boolean onTouchEvent(MotionEvent event) {
            pinch.onTouchEvent(event);
            int action = event.getActionMasked();
            if (action == MotionEvent.ACTION_DOWN) { getParent().requestDisallowInterceptTouchEvent(true); lastX = startX = event.getX(); lastY = startY = event.getY(); dragging = true; scaled = false; scale = 1; }
            else if (action == MotionEvent.ACTION_MOVE && dragging) {
                if (!pinch.isInProgress() && !scaled && event.getPointerCount() == 1 && getWidth() > 0 && getHeight() > 0)
                    PocketDualScreen.send("pan", "", (event.getX() - lastX) / getWidth(), (event.getY() - lastY) / getHeight());
                lastX = event.getX(); lastY = event.getY();
            } else if (action == MotionEvent.ACTION_UP || action == MotionEvent.ACTION_CANCEL) {
                if (action == MotionEvent.ACTION_UP && !scaled && Math.hypot(event.getX() - startX, event.getY() - startY) < dp(10)) { markerName(event.getX(), event.getY()); performClick(); }
                dragging = false; getParent().requestDisallowInterceptTouchEvent(false);
            }
            return true; // Native secondary touches never propagate into Unity combat/camera input.
        }
        public boolean performClick() { super.performClick(); return true; }
        private void markerName(float px, float py) {
            JSONArray marks = data.optJSONArray("markers"); if (marks == null) return;
            float x = (float)data.optDouble("x"), y = (float)data.optDouble("y"), rw = (float)data.optDouble("width", 1), rh = (float)data.optDouble("height", 1);
            for (int i = marks.length() - 1; i >= 0; i--) {
                JSONObject m = marks.optJSONObject(i); if (m == null) continue;
                float mx = ((float)m.optDouble("x") - x) / rw * getWidth(), my = (1 - ((float)m.optDouble("y") - y) / rh) * getHeight();
                if (Math.hypot(px - mx, py - my) < dp(20)) { location.setText(m.optString("name")); markerLabelUntil=android.os.SystemClock.uptimeMillis()+5000; break; }
            }
        }
        void release() { released = true; decoder.shutdownNow(); images.evictAll(); }
    }
}
