package com.openfusion.dualscreen;

import android.app.*;
import android.content.*;
import android.graphics.*;
import android.os.*;
import android.security.keystore.*;
import android.util.AtomicFile;
import android.view.*;
import android.widget.PopupWindow;
import android.graphics.drawable.ColorDrawable;
import java.io.*;
import java.security.*;
import javax.crypto.*;
import javax.crypto.spec.GCMParameterSpec;

/** Private encrypted credentials and UI input; never logs account data. */
public final class PocketPlatform {
    public static void shareDiagnostics(Activity activity, String report) {
        main.post(() -> {
            Intent share = new Intent(Intent.ACTION_SEND);
            share.setType("text/plain");
            share.putExtra(Intent.EXTRA_SUBJECT, "OpenFusion Pocket diagnostics");
            share.putExtra(Intent.EXTRA_TEXT, report);
            activity.startActivity(Intent.createChooser(share, "Share diagnostics"));
        });
    }
    private static volatile int hatDirections;
    public static void navigationMotion(MotionEvent event) {
        if (!event.isFromSource(InputDevice.SOURCE_JOYSTICK) || event.getActionMasked()!=MotionEvent.ACTION_MOVE) return;
        float x=event.getAxisValue(MotionEvent.AXIS_HAT_X), y=event.getAxisValue(MotionEvent.AXIS_HAT_Y);
        hatDirections=(x<-.55f?1:0)|(x>.55f?2:0)|(y<-.55f?4:0)|(y>.55f?8:0);
    }
    public static void resetNavigation() { hatDirections=0; }
    public static int navigationHat() { return hatDirections; }
    private static final Handler main=new Handler(Looper.getMainLooper());
    private static final String ALIAS="OpenFusion.Pocket.Credentials.v1";
    private static javax.crypto.SecretKey key() throws Exception {
        KeyStore store=KeyStore.getInstance("AndroidKeyStore");store.load(null);
        if(!store.containsAlias(ALIAS)){
            KeyGenerator generator=KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore");
            generator.init(new KeyGenParameterSpec.Builder(ALIAS,KeyProperties.PURPOSE_ENCRYPT|KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
            generator.generateKey();
        }
        return (javax.crypto.SecretKey)store.getKey(ALIAS,null);
    }
    private static File file(Context c,String identity) throws Exception {
        byte[] digest=MessageDigest.getInstance("SHA-256").digest(identity.getBytes("UTF-8"));
        StringBuilder name=new StringBuilder();for(byte b:digest)name.append(String.format(java.util.Locale.ROOT,"%02x",b&255));
        File dir=new File(c.getNoBackupFilesDir(),"pocket-credentials");if(!dir.isDirectory()&&!dir.mkdirs())throw new IOException();
        return new File(dir,name+".bin");
    }
    public static synchronized boolean save(Context c,String identity,String password){
        AtomicFile target=null;FileOutputStream output=null;
        try{
            if(identity==null||identity.isEmpty()||password==null||password.length()>4096)return false;
            target=new AtomicFile(file(c,identity));Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.ENCRYPT_MODE,key());cipher.updateAAD(identity.getBytes("UTF-8"));
            byte[] encrypted=cipher.doFinal(password.getBytes("UTF-8"));output=target.startWrite();output.write(cipher.getIV());output.write(encrypted);target.finishWrite(output);return true;
        }catch(Exception e){if(target!=null&&output!=null)target.failWrite(output);return false;}
    }
    public static synchronized String load(Context c,String identity){
        try{
            AtomicFile target=new AtomicFile(file(c,identity));byte[] data=target.readFully();if(data.length<28||data.length>8192)return "";
            Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.DECRYPT_MODE,key(),new GCMParameterSpec(128,data,0,12));cipher.updateAAD(identity.getBytes("UTF-8"));
            return new String(cipher.doFinal(data,12,data.length-12),"UTF-8");
        }catch(Exception e){return "";}
    }
    public static synchronized void forget(Context c,String identity){try{new AtomicFile(file(c,identity)).delete();}catch(Exception ignored){}}
    public static synchronized void forgetAll(Context c){File dir=new File(c.getNoBackupFilesDir(),"pocket-credentials");File[] files=dir.listFiles();if(files!=null)for(File f:files)if(f.isFile())f.delete();}
    public static boolean selfTest(Context c){
        if(c instanceof Activity)pointerDiagnostics((Activity)c,0);
        String identity="fixture-"+java.util.UUID.randomUUID(),password="synthetic-credential-only";
        try{
            if(!save(c,identity,password)||!password.equals(load(c,identity)))return false;
            byte[] encrypted=new AtomicFile(file(c,identity)).readFully();
            if(new String(encrypted,"ISO-8859-1").contains(password))return false;
            encrypted[encrypted.length-1]^=1;
            try(FileOutputStream out=new FileOutputStream(file(c,identity))){out.write(encrypted);}
            if(!load(c,identity).isEmpty())return false;
            forget(c,identity);return !file(c,identity).exists();
        }catch(Exception e){return false;}finally{forget(c,identity);}
    }
    public static void ready(Context c){c.getSharedPreferences("pocket.renderer",0).edit().putBoolean("starting",false).apply();}
    public static void rendererSettings(Activity a){main.post(()->{
        SharedPreferences prefs=a.getSharedPreferences("pocket.renderer",0);int current=prefs.getBoolean("vulkan",false)?1:0;
        new AlertDialog.Builder(a).setTitle("Graphics API (restart required)")
          .setSingleChoiceItems(new String[]{"OpenGL ES 3 (default)","Vulkan (experimental)"},current,(dialog,which)->{
            dialog.dismiss();
            if(which==1&&!a.getPackageManager().hasSystemFeature("android.hardware.vulkan.version")){
                new AlertDialog.Builder(a).setMessage("Android does not report Vulkan support. OpenGL remains selected.").setPositiveButton("OK",null).show();return;
            }
            new AlertDialog.Builder(a).setTitle("Apply on next app launch?")
              .setMessage("Save the graphics preference, then fully close and reopen the app. Vulkan may render differently or run slower on some devices. OpenGL remains available. An unfinished engine startup falls back to OpenGL on the next launch.")
              .setNegativeButton("Cancel",null).setPositiveButton("Save",(d,w)->prefs.edit().putBoolean("vulkan",which==1).putBoolean("starting",false).apply()).show();
          }).setNegativeButton("Close",null).show();
    });}
    private static ViewGroup surface;private static Cursor cursor;private static PopupWindow cursorWindow;private static int surfaceWidth,surfaceHeight,windowX=Integer.MIN_VALUE,windowY=Integer.MIN_VALUE;private static boolean pressed;private static long downAt;
    private static volatile int pointerCalls;private static volatile String pointerReason="not-called";
    private static void pointerDiagnostics(Activity a,int sample){main.postDelayed(()->{
        int pads=0;for(int id:InputDevice.getDeviceIds()){InputDevice device=InputDevice.getDevice(id);if(device!=null&&((device.getSources()&InputDevice.SOURCE_JOYSTICK)==InputDevice.SOURCE_JOYSTICK))pads++;}
        android.util.Log.i("PocketPointer","calls="+pointerCalls+" state="+pointerReason+" focus="+a.hasWindowFocus()+" physical="+physicalHolding+" pads="+pads+" cursor="+(cursor!=null)+" window="+(cursorWindow!=null&&cursorWindow.isShowing())+" draws="+(cursor==null?0:cursor.draws)+" surface="+(surface==null?"none":surface.getWidth()+"x"+surface.getHeight()));
        if(sample<9&&!a.isFinishing())pointerDiagnostics(a,sample+1);
    },2000);}
    private static boolean physicalHolding;private static long physicalAt;
    public static void physicalTouch(MotionEvent event){
        if(!event.isFromSource(InputDevice.SOURCE_TOUCHSCREEN))return;
        int action=event.getActionMasked();
        if(action==MotionEvent.ACTION_DOWN||action==MotionEvent.ACTION_POINTER_DOWN||action==MotionEvent.ACTION_MOVE)physicalHolding=true;
        else if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_CANCEL)physicalHolding=false;
        else return;
        physicalAt=SystemClock.uptimeMillis();
    }
    private static final class Cursor extends View {
        float x,y;int draws;final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        Cursor(Context c){super(c);setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);}
        protected void onDraw(Canvas canvas){draws++;float cx=getWidth()*.5f,cy=getHeight()*.5f,r=Math.min(cx,cy)*.35f;p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(4);p.setColor(Color.BLACK);canvas.drawCircle(cx,cy,r+2,p);p.setStrokeWidth(2);p.setColor(Color.CYAN);canvas.drawCircle(cx,cy,r,p);canvas.drawLine(cx-r*1.5f,cy,cx+r*1.5f,cy,p);canvas.drawLine(cx,cy-r*1.5f,cx,cy+r*1.5f,p);}
    }
    private static void removeCursor(){
        if(cursorWindow!=null){try{cursorWindow.dismiss();}catch(RuntimeException ignored){}}
        cursorWindow=null;cursor=null;surface=null;surfaceWidth=surfaceHeight=0;windowX=windowY=Integer.MIN_VALUE;
    }
    private static boolean placeCursor(){
        int size=Math.max(40,Math.round(28*surface.getResources().getDisplayMetrics().density));
        int[] location=new int[2];surface.getLocationOnScreen(location);int x=location[0]+Math.round(cursor.x)-size/2,y=location[1]+Math.round(cursor.y)-size/2;
        try{
            if(cursorWindow==null){
                cursorWindow=new PopupWindow(surface.getContext());cursorWindow.setContentView(cursor);cursorWindow.setWidth(size);cursorWindow.setHeight(size);
                cursorWindow.setFocusable(false);cursorWindow.setTouchable(false);cursorWindow.setOutsideTouchable(false);cursorWindow.setClippingEnabled(false);
                cursorWindow.setInputMethodMode(PopupWindow.INPUT_METHOD_NOT_NEEDED);cursorWindow.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));
                cursorWindow.showAtLocation(surface,Gravity.TOP|Gravity.LEFT,x,y);
            }else if(windowX!=x||windowY!=y)cursorWindow.update(x,y,-1,-1);
            windowX=x;windowY=y;return true;
        }catch(RuntimeException e){pointerReason="window-unavailable";cancel();removeCursor();return false;}
    }
    private static void cancel(){if(surface!=null&&pressed){MotionEvent e=MotionEvent.obtain(downAt,SystemClock.uptimeMillis(),MotionEvent.ACTION_CANCEL,0,0,0);surface.dispatchTouchEvent(e);e.recycle();}pressed=false;}
    public static void pointer(Activity a,boolean secondary,int action,float nx,float ny,float scroll){pointerCalls++;main.post(()->{
        if(!a.hasWindowFocus()&&action!=4){pointerReason="no-focus";physicalHolding=false;cancel();removeCursor();return;}
        // Real fingers take priority over an idle gamepad cursor. Synthetic events
        // below dispatch directly to the view and do not traverse the activity.
        if(action!=4&&(physicalHolding||(action==-1&&SystemClock.uptimeMillis()-physicalAt<600))){
            pointerReason="physical-touch";
            cancel();removeCursor();return;
        }
        View found=PocketDualScreen.pointerSurface(a,secondary);ViewGroup next=found instanceof ViewGroup?(ViewGroup)found:null;
        if(action==4||next!=surface){cancel();removeCursor();}
        if(action==4||next==null){pointerReason=action==4?"hidden":"no-surface";return;}
        pointerReason="shown";
        if(surface==null){surface=next;cursor=new Cursor(surface.getContext());}
        int w=surface.getWidth(),h=surface.getHeight();if(w<=0||h<=0){cancel();return;}
        if(surfaceWidth>0&&(surfaceWidth!=w||surfaceHeight!=h))cancel();surfaceWidth=w;surfaceHeight=h;
        cursor.x=Math.max(1,Math.min(w-1,nx*w));cursor.y=Math.max(1,Math.min(h-1,ny*h));if(!placeCursor())return;
        long now=SystemClock.uptimeMillis();
        if(action==-1&&!pressed){MotionEvent e=MotionEvent.obtain(now,now,MotionEvent.ACTION_HOVER_MOVE,cursor.x,cursor.y,0);e.setSource(InputDevice.SOURCE_MOUSE);surface.dispatchGenericMotionEvent(e);e.recycle();}
        if(action==0){if(pressed)return;downAt=now;pressed=true;}
        if(action==1&&!pressed)return;
        if(action==3){cancel();return;}
        if(action==5){
            MotionEvent.PointerProperties prop=new MotionEvent.PointerProperties();prop.id=0;prop.toolType=MotionEvent.TOOL_TYPE_MOUSE;
            MotionEvent.PointerCoords coords=new MotionEvent.PointerCoords();coords.x=cursor.x;coords.y=cursor.y;coords.setAxisValue(MotionEvent.AXIS_VSCROLL,scroll);
            MotionEvent e=MotionEvent.obtain(now,now,MotionEvent.ACTION_SCROLL,1,new MotionEvent.PointerProperties[]{prop},new MotionEvent.PointerCoords[]{coords},0,0,1,1,0,0,InputDevice.SOURCE_MOUSE,0);surface.dispatchGenericMotionEvent(e);e.recycle();return;
        }
        if(action>=0&&action<=2&&(action!=2||pressed)){MotionEvent e=MotionEvent.obtain(downAt,now,action,cursor.x,cursor.y,0);e.setSource(InputDevice.SOURCE_TOUCHSCREEN);surface.dispatchTouchEvent(e);e.recycle();if(action==1)pressed=false;}
    });}
    public static void back(Activity a){main.post(()->{View v=PocketDualScreen.pointerSurface(a,false);if(v!=null){v.dispatchKeyEvent(new KeyEvent(KeyEvent.ACTION_DOWN,KeyEvent.KEYCODE_BACK));v.dispatchKeyEvent(new KeyEvent(KeyEvent.ACTION_UP,KeyEvent.KEYCODE_BACK));}});}
}
