package com.openfusion.dualscreen;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Presentation;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Rect;
import android.hardware.display.DisplayManager;
import android.hardware.input.InputManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.Display;
import android.view.Gravity;
import android.view.InputDevice;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import androidx.core.util.Consumer;
import androidx.window.java.layout.WindowInfoTrackerCallbackAdapter;
import androidx.window.layout.DisplayFeature;
import androidx.window.layout.FoldingFeature;
import androidx.window.layout.WindowInfoTracker;
import androidx.window.layout.WindowLayoutInfo;
import org.json.JSONObject;
import java.util.concurrent.ConcurrentLinkedQueue;

/** Native panels own their MotionEvents. Unity's actual surface is resized, so all legacy
 * Screen/input/camera coordinates stay in one coordinate system, including the HUD. */
public final class PocketDualScreen {
    private static final Handler main = new Handler(Looper.getMainLooper());
    private static final ConcurrentLinkedQueue<String> commands = new ConcurrentLinkedQueue<>();
    private static volatile String current = "{}";
    private static PocketDualScreen instance;
    private static Activity host;
    private static PreviewFrame pendingPreview;
    private static boolean previewPosted;
    private static final class PreviewFrame {
        final int revision; final int[] ids,pixels;
        PreviewFrame(int r,int[] i,int[] p) { revision=r; ids=i; pixels=p; }
    }
    public static void nanoFrame(int revision,int[] ids,int[] pixels) {
        if(ids==null||ids.length!=4||pixels==null||pixels.length!=512*128) return;
        synchronized(PocketDualScreen.class) {
            pendingPreview=new PreviewFrame(revision,ids,pixels);
            if(previewPosted) return; previewPosted=true;
        }
        main.post(() -> {
            PreviewFrame frame;
            synchronized(PocketDualScreen.class) { frame=pendingPreview; pendingPreview=null; previewPosted=false; }
            if(frame!=null && instance!=null && !instance.stopped && instance.active && instance.revision==frame.revision && instance.panel!=null)
                instance.panel.nanoFrame(frame.ids,frame.pixels);
        });
    }
    private static int[] pendingAvatar;
    private static int avatarRevision;
    private static boolean avatarPosted;
    public static void avatarFrame(int revision,int[] pixels) {
        if(pixels==null || pixels.length!=256*320)return;
        synchronized(PocketDualScreen.class) {
            pendingAvatar=pixels;avatarRevision=revision;
            if(avatarPosted)return;avatarPosted=true;
        }
        main.post(() -> {
            int[] frame;int rev;
            synchronized(PocketDualScreen.class){frame=pendingAvatar;rev=avatarRevision;pendingAvatar=null;avatarPosted=false;}
            if(frame!=null && instance!=null && !instance.stopped && instance.active && instance.revision==rev && instance.panel!=null)
                instance.panel.avatarFrame(frame);
        });
    }
    private final Activity activity;
    private final FrameLayout parent;
    private final View unity;
    private final FrameLayout.LayoutParams original;
    private final DisplayManager displays;
    private final InputManager inputs;
    private final SharedPreferences touchProof;
    private String sessionTouch = "";
    private WindowInfoTrackerCallbackAdapter tracker;
    private FoldingFeature fold;
    private boolean observed, wanted, active, stopped, probing;
    private int kind, verifiedDisplay = -1, candidate = -1, revision;
    private String reason = "Waiting for Android window information.";
    private final Rect primary = new Rect(), secondary = new Rect();
    private Rect applied = new Rect();
    private Rect appliedSecondary = new Rect();
    private Presentation presentation;
    private DualPanel panel;
    private String lastPanel = "{}";
    private AlertDialog settingsDialog;
    private TextView settingsInfo;
    private Button dualOption;
    private final Consumer<WindowLayoutInfo> listener = info -> {
        fold = null;
        for (DisplayFeature f : info.getDisplayFeatures()) if (f instanceof FoldingFeature) { fold = (FoldingFeature)f; break; }
        observed = true; refresh();
    };
    private final DisplayManager.DisplayListener displayListener = new DisplayManager.DisplayListener() {
        public void onDisplayAdded(int id) { refresh(); }
        public void onDisplayChanged(int id) { refresh(); }
        public void onDisplayRemoved(int id) {
            if(touchProof.getInt("display",-1)==id) touchProof.edit().clear().apply();
            if (verifiedDisplay == id) verifiedDisplay = -1;
            if (presentation != null && presentation.getDisplay().getDisplayId() == id) dismissPresentation();
            refresh();
        }
    };
    private final InputManager.InputDeviceListener inputListener = new InputManager.InputDeviceListener() {
        public void onInputDeviceAdded(int id) { refresh(); }
        public void onInputDeviceChanged(int id) { refresh(); }
        public void onInputDeviceRemoved(int id) { refresh(); }
    };
    private final android.view.ViewTreeObserver.OnGlobalLayoutListener layoutListener = this::refresh;

    public static void start(Activity activity) { main.post(() -> {
        host = activity;
        if (instance != null) instance.close();
        try { instance = new PocketDualScreen(activity); }
        catch (Exception | LinkageError e) { publishFailure("The Unity surface cannot be safely resized on this device."); }
    }); }
    public static String status() { return current; }
    public static String command() { String value = commands.poll(); return value == null ? "" : value; }
    public static void setActive(boolean enabled) { main.post(() -> { if (instance != null) { instance.wanted = enabled; instance.refresh(); } }); }
    public static void panel(String data) { main.post(() -> { if (instance != null) { instance.lastPanel = data; if (instance.panel != null) instance.panel.update(data); } }); }
    public static void settings(int preference) { main.post(() -> {
        if (instance != null) instance.showSettings(preference);
        else if (host != null) new AlertDialog.Builder(host).setTitle("Dual Screen unavailable").setMessage("Android could not safely create the secondary surface. Single Screen remains active.").setPositiveButton("Close", null).show();
    }); }
    public static void stop() { main.post(() -> { if (instance != null) { instance.close(); instance = null; } host = null; commands.clear(); }); }
    static void send(String action, String value, float x, float y) {
        try { JSONObject o = new JSONObject(); o.put("action", action); o.put("value", value); o.put("x", x); o.put("y", y); o.put("revision", instance != null ? instance.revision : -1); if (commands.size() >= 128) commands.poll(); commands.offer(o.toString()); } catch (Exception ignored) { }
    }
    private static void publishFailure(String reason) {
        try { current = new JSONObject().put("reliable", false).put("active", false).put("reason", reason).toString(); } catch (Exception ignored) { }
    }
    private PocketDualScreen(Activity a) {
        activity = a;
        View content = a.findViewById(android.R.id.content);
        unity = findUnity(content);
        if (unity == null || !(unity.getParent() instanceof FrameLayout)) throw new IllegalStateException("Unsupported Unity hierarchy");
        parent = (FrameLayout)unity.getParent();
        original = new FrameLayout.LayoutParams((FrameLayout.LayoutParams)unity.getLayoutParams());
        displays = (DisplayManager)a.getSystemService(Context.DISPLAY_SERVICE);
        inputs = (InputManager)a.getSystemService(Context.INPUT_SERVICE);
        touchProof = a.getSharedPreferences("pocket.ds.touchProof.v1", Context.MODE_PRIVATE);
        inputs.registerInputDeviceListener(inputListener, main);
        displays.registerDisplayListener(displayListener, main);
        parent.getViewTreeObserver().addOnGlobalLayoutListener(layoutListener);
        try {
            tracker = new WindowInfoTrackerCallbackAdapter(WindowInfoTracker.getOrCreate(a));
            tracker.addWindowLayoutInfoListener(a, main::post, listener);
        } catch (Exception | LinkageError e) { tracker = null; reason = "Android does not provide reliable fold information."; }
        refresh();
    }
    public static View pointerSurface(Activity activity,boolean secondary) {
        if(secondary)return instance!=null&&instance.active?instance.panel:null;
        return findUnity(activity.getWindow().getDecorView());
    }
    private static View findUnity(View view) {
        if (view.getClass().getName().equals("com.unity3d.player.UnityPlayer")) return view;
        if (view instanceof ViewGroup) { ViewGroup group = (ViewGroup)view; for (int i = 0; i < group.getChildCount(); i++) { View found = findUnity(group.getChildAt(i)); if (found != null) return found; } }
        return null;
    }
    private Rect usable() {
        Rect r = new Rect(0, 0, parent.getWidth(), parent.getHeight());
        WindowInsets insets = parent.getRootWindowInsets();
        if (insets != null) {
            // Stable insets avoid treating the IME height as a hinge or a smaller screen.
            r.left += insets.getStableInsetLeft(); r.top += insets.getStableInsetTop();
            r.right -= insets.getStableInsetRight(); r.bottom -= insets.getStableInsetBottom();
            if (Build.VERSION.SDK_INT >= 28 && insets.getDisplayCutout() != null) {
                android.view.DisplayCutout c = insets.getDisplayCutout();
                r.left = Math.max(r.left, c.getSafeInsetLeft()); r.top = Math.max(r.top, c.getSafeInsetTop());
                r.right = Math.min(r.right, parent.getWidth() - c.getSafeInsetRight()); r.bottom = Math.min(r.bottom, parent.getHeight() - c.getSafeInsetBottom());
            }
        }
        return r;
    }
    private boolean fits(Rect p, Rect s, float d) {
        return p.width() / d >= 480 && p.height() / d >= 270 && s.width() / d >= 320 && s.height() / d >= 300 && p.width() >= p.height() * 1.2f;
    }
    private String[] touchDescriptors() {
        java.util.ArrayList<String> result=new java.util.ArrayList<>();
        for(int id: inputs.getInputDeviceIds()) {
            InputDevice device=inputs.getInputDevice(id);
            if(device!=null && !device.isVirtual() && device.supportsSource(InputDevice.SOURCE_TOUCHSCREEN)) result.add(device.getDescriptor());
        }
        return result.toArray(new String[0]);
    }
    private boolean hasTouch(String descriptor) {
        int count=0; for(String d:touchDescriptors()) if(!descriptor.isEmpty() && descriptor.equals(d)) count++;
        return count==1;
    }
    private int bootCount() {
        return Build.VERSION.SDK_INT>=24 ? Settings.Global.getInt(activity.getContentResolver(),Settings.Global.BOOT_COUNT,-1) : -1;
    }
    private String surfaceKey(Display display) {
        android.util.DisplayMetrics m=new android.util.DisplayMetrics(); display.getMetrics(m);
        Display.Mode mode=display.getMode(); String name=display.getName();
        // Rotation and refresh-rate changes preserve identity; a different mode/density does not.
        return name.length()+":"+name+":"+Math.min(mode.getPhysicalWidth(),mode.getPhysicalHeight())+":"+
            Math.max(mode.getPhysicalWidth(),mode.getPhysicalHeight())+":"+m.densityDpi+":"+display.getFlags();
    }
    private boolean rememberedTouch(Display display) {
        return PhysicalTouchProof.matches(touchProof.getInt("boot",-1),bootCount(),touchProof.getInt("display",-1),display.getDisplayId(),
            touchProof.getString("surface",""),surfaceKey(display),touchProof.getString("touch",""),touchDescriptors());
    }
    private void rememberTouch(Display display, InputDevice device) {
        sessionTouch=device.getDescriptor();
        if(bootCount()<0) return; // Older/unknown Android boot identity: session-only verification.
        touchProof.edit().putInt("boot",bootCount()).putInt("display",display.getDisplayId()).putString("surface",surfaceKey(display))
            .putString("touch",sessionTouch).apply();
    }
    private void discover() {
        kind = 0; primary.setEmpty(); secondary.setEmpty(); candidate = -1;
        float d = activity.getResources().getDisplayMetrics().density;
        Rect area = usable();
        reason = observed ? "No usable fold or verified touch display. Single Screen uses the entire game surface." : "Android has not confirmed a usable fold configuration.";
        if (fold != null) {
            boolean flat = fold.getState() == FoldingFeature.State.FLAT;
            boolean split = fold.isSeparating() || fold.getOcclusionType() == FoldingFeature.OcclusionType.FULL;
            Rect hinge = new Rect(fold.getBounds()); int[] location = new int[2]; parent.getLocationInWindow(location); hinge.offset(-location[0], -location[1]);
            DualScreenLayout.Plan plan = DualScreenLayout.fold(new DualScreenLayout.Box(area.left, area.top, area.right, area.bottom), d, observed, flat, split,
                fold.getOrientation() == FoldingFeature.Orientation.HORIZONTAL, new DualScreenLayout.Box(hinge.left, hinge.top, hinge.right, hinge.bottom));
            if (plan.usable) {
                primary.set(plan.primary.left, plan.primary.top, plan.primary.right, plan.primary.bottom);
                secondary.set(plan.secondary.left, plan.secondary.top, plan.secondary.right, plan.secondary.bottom);
                kind = 1; reason = "Compatible fold layout detected. Secondary map uses live game data."; return;
            }
            reason = "The current fold posture has insufficient usable space. Open the inner screen fully or use Single Screen.";
        }
        Display own = parent.getDisplay();
        if(verifiedDisplay>=0 && !hasTouch(sessionTouch)) verifiedDisplay=-1;
        for (Display display : displays.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION)) {
            if (!display.isValid() || own == null || own.getDisplayId() == display.getDisplayId() || display.getState() != Display.STATE_ON) continue;
            android.util.DisplayMetrics metrics = new android.util.DisplayMetrics(); display.getMetrics(metrics);
            Rect p = new Rect(area), s = new Rect(0, 0, metrics.widthPixels, metrics.heightPixels);
            if (p.width() / d < 480 || p.height() / d < 270 || p.width() < p.height() * 1.2f || s.width() / metrics.density < 320 || s.height() / metrics.density < 300) continue;
            candidate = display.getDisplayId();
            reason = "A presentation display is present. Test touch once for this connection; it will be remembered when reopening the app.";
            if(verifiedDisplay!=candidate && rememberedTouch(display)) {
                verifiedDisplay=candidate; sessionTouch=touchProof.getString("touch","");
            }
            if (verifiedDisplay == candidate) { kind = 2; primary.set(p); secondary.set(s); reason = "Secondary display and its touch input were verified. Automatic mode can reuse this connection after reopening the app."; return; }
        }
        if (candidate < 0) verifiedDisplay = -1;
    }
    private void refresh() {
        if (stopped) return;
        try {
            discover();
            boolean enable = wanted && kind != 0 && !probing && activity.hasWindowFocus();
            if (enable && (!active || !applied.equals(primary) || !appliedSecondary.equals(secondary) || (kind == 2) != (presentation != null))) {
                restore();
                if (kind == 1) {
                    panel = new DualPanel(activity);
                    FrameLayout.LayoutParams params = new FrameLayout.LayoutParams(secondary.width(), secondary.height(), Gravity.TOP | Gravity.LEFT);
                    params.leftMargin = secondary.left; params.topMargin = secondary.top; parent.addView(panel, params);
                    FrameLayout.LayoutParams game = new FrameLayout.LayoutParams(primary.width(), primary.height(), Gravity.TOP | Gravity.LEFT);
                    game.leftMargin = primary.left; game.topMargin = primary.top; unity.setLayoutParams(game);
                } else {
                    Display display = displays.getDisplay(verifiedDisplay);
                    if (display == null || !display.isValid()) throw new IllegalStateException("Display disconnected");
                    presentation = new Presentation(activity, display); panel = new DualPanel(presentation.getContext());
                    presentation.setContentView(panel);
                    // Keep primary gameplay focus and gamepad routing on the Unity activity.
                    presentation.getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE);
                    presentation.show();
                }
                panel.update(lastPanel); applied.set(primary); appliedSecondary.set(secondary); active = true; revision++;
            } else if (!enable && active) { restore(); revision++; }
            publish();
            if (settingsDialog != null && settingsDialog.isShowing()) { settingsInfo.setText(settingsText()); dualOption.setEnabled(kind != 0); }
        } catch (Exception | LinkageError e) {
            android.util.Log.e("PocketDS", "Secondary surface creation failed", e);
            wanted = false; restore(); kind = 0; reason = "Android could not create the secondary surface. Single Screen was restored."; publish();
        }
    }
    private void publish() {
        try {
            float d = activity.getResources().getDisplayMetrics().density;
            // Express external dimensions in primary-density pixels for the shared policy.
            int sw = secondary.width(), sh = secondary.height();
            if (kind == 2) { android.util.DisplayMetrics m = new android.util.DisplayMetrics(); Display v = displays.getDisplay(verifiedDisplay); if (v != null) { v.getMetrics(m); sw = (int)Math.ceil(sw / m.density * d); sh = (int)Math.ceil(sh / m.density * d); } }
            JSONObject o = new JSONObject();
            o.put("reliable", observed || kind == 2); o.put("available", kind != 0); o.put("active", active); o.put("kind", kind); o.put("revision", revision);
            o.put("fold", fold != null); o.put("flat", fold != null && fold.getState() == FoldingFeature.State.FLAT); o.put("separating", fold != null && fold.isSeparating());
            o.put("physical", kind == 2); o.put("touchVerified", verifiedDisplay >= 0); o.put("density", d);
            o.put("width", parent.getWidth()); o.put("height", parent.getHeight()); o.put("primaryWidth", primary.width()); o.put("primaryHeight", primary.height());
            o.put("secondaryWidth", sw); o.put("secondaryHeight", sh); o.put("reason", reason);
            String next = o.toString(); if (!next.equals(current)) android.util.Log.i("PocketDS", next); current = next;
        } catch (Exception ignored) { publishFailure(reason); }
    }
    private void dismissPresentation() { if (presentation != null) { presentation.dismiss(); presentation = null; } }
    private void restore() {
        if (panel != null) { panel.release(); if (panel.getParent() == parent) parent.removeView(panel); panel = null; }
        if (!probing) dismissPresentation();
        if (active || !applied.isEmpty()) unity.setLayoutParams(new FrameLayout.LayoutParams(original));
        active = false; applied.setEmpty(); appliedSecondary.setEmpty();
    }
    private void showSettings(int preference) {
        if (settingsDialog != null && settingsDialog.isShowing()) return;
        LinearLayout body = new LinearLayout(activity); body.setOrientation(LinearLayout.VERTICAL); int pad = (int)(20 * activity.getResources().getDisplayMetrics().density); body.setPadding(pad, pad, pad, pad);
        TextView info = new TextView(activity); info.setText(settingsText()); body.addView(info); settingsInfo = info;
        android.widget.ScrollView scroll = new android.widget.ScrollView(activity); scroll.addView(body);
        AlertDialog dialog = new AlertDialog.Builder(activity).setTitle("Dual Screen").setView(scroll).setNegativeButton("Close", null).create(); settingsDialog = dialog;
        dialog.setOnDismissListener(d -> { settingsDialog = null; settingsInfo = null; dualOption = null; });
        String[] labels = { "Automatic", "Single Screen", "Dual Screen" };
        for (int i = 0; i < labels.length; i++) {
            final int option = i; Button button = new Button(activity); button.setText(labels[i] + (preference == i ? "  ✓" : ""));
            button.setEnabled(i != 2 || kind != 0);
            if (i == 2) dualOption = button;
            button.setOnClickListener(v -> { send("preference", Integer.toString(option), 0, 0); dialog.dismiss(); }); body.addView(button);
        }
        if (candidate >= 0 && verifiedDisplay != candidate) {
            Button test = new Button(activity); test.setText("Test secondary touch display"); test.setOnClickListener(v -> { dialog.dismiss(); probe(); }); body.addView(test);
        }
        dialog.show();
    }
    private String settingsText() { return reason + "\n\nMap, Nanos, equipment, missions and E-Mail use live secondary panels. Chat and the mail composer open on the main screen. Visit a Nano Station to change Nano equipment or powers."; }
    private void probe() {
        Display display = displays.getDisplay(candidate); if (display == null) return;
        restore(); probing = true;
        try {
            final int id = candidate;
            presentation = new Presentation(activity, display);
            Button confirm = new Button(presentation.getContext()); confirm.setText("OpenFusion:Pocket\nTouch HERE on this display to confirm independent touch input.\nThis test expires in 15 seconds.");
            confirm.setOnTouchListener((v, event) -> {
                InputDevice device=event.getDevice();
                if (event.getActionMasked() == MotionEvent.ACTION_UP && event.isFromSource(InputDevice.SOURCE_TOUCHSCREEN) &&
                    device!=null && !device.isVirtual() && device.supportsSource(InputDevice.SOURCE_TOUCHSCREEN) && hasTouch(device.getDescriptor())) {
                    rememberTouch(display,device); verifiedDisplay = id; probing = false; dismissPresentation(); refresh(); return true;
                }
                return true;
            });
            presentation.setContentView(confirm); presentation.getWindow().addFlags(android.view.WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE); presentation.show();
            main.postDelayed(() -> { if (!stopped && probing) { probing = false; dismissPresentation(); refresh(); } }, 15000);
        } catch (Exception e) { probing = false; dismissPresentation(); refresh(); }
    }
    private void close() {
        stopped = true; probing = false; restore(); displays.unregisterDisplayListener(displayListener);
        inputs.unregisterInputDeviceListener(inputListener);
        if (settingsDialog != null) settingsDialog.dismiss();
        parent.getViewTreeObserver().removeOnGlobalLayoutListener(layoutListener);
        if (tracker != null) tracker.removeWindowLayoutInfoListener(listener);
        publishFailure("Dual Screen stopped. Single Screen restored.");
    }
}
