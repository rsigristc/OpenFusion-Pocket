package com.openfusion.pocket.tests;

import android.app.Activity;
import android.app.Instrumentation;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.os.Bundle;
import android.view.ContextThemeWrapper;
import android.view.View;
import android.view.ViewGroup;
import java.lang.reflect.Constructor;
import java.lang.reflect.Method;

/** Runs the actual APK's native Companion constructor and drawing on Android's UI thread. */
public final class PanelSmoke extends Instrumentation {
    private boolean requireReport;
    private String reportCheck="";
    @Override public void onCreate(Bundle arguments) { super.onCreate(arguments); requireReport=arguments!=null&&"true".equals(arguments.getString("requireReport")); start(); }
    @Override public void onStart() {
        Bundle result = new Bundle();
        final Throwable[] failure = new Throwable[1];
        runOnMainSync(() -> {
            View root = null;
            Method release = null;
            try {
                Context context = new ContextThemeWrapper(getTargetContext(), android.R.style.Theme_Material_Light_NoActionBar);
                diagnosticsSmoke(context);
                Class<?> panel = getTargetContext().getClassLoader().loadClass("com.openfusion.dualscreen.DualPanel");
                Constructor<?> constructor = panel.getDeclaredConstructor(Context.class); constructor.setAccessible(true);
                release = panel.getDeclaredMethod("release"); release.setAccessible(true);
                root = (View)constructor.newInstance(context);
                ViewGroup group = (ViewGroup)root;
                if (group.getChildCount() != 4 || ((ViewGroup)group.getChildAt(0)).getChildCount() != 4)
                    throw new AssertionError("Companion header, boost strip and camera controls must each attach once");
                for (int[] size : new int[][] {{1430, 1182}, {1280, 720}}) {
                    root.measure(View.MeasureSpec.makeMeasureSpec(size[0], View.MeasureSpec.EXACTLY),
                        View.MeasureSpec.makeMeasureSpec(size[1], View.MeasureSpec.EXACTLY));
                    root.layout(0, 0, size[0], size[1]);
                    Bitmap frame = Bitmap.createBitmap(size[0], size[1], Bitmap.Config.ARGB_8888);
                    try { root.draw(new Canvas(frame)); } finally { frame.recycle(); }
                    if (group.getChildAt(0).getWidth() <= 0) throw new AssertionError("Header has no usable width");
                }
            } catch (Throwable error) {
                failure[0] = error.getCause() == null ? error : error.getCause();
            } finally {
                if (root != null && release != null) try { release.invoke(root); } catch (Throwable error) { failure[0] = error; }
            }
        });
        if (failure[0] != null) {
            result.putString("stream", "FAIL: " + android.util.Log.getStackTraceString(failure[0]));
            finish(Activity.RESULT_CANCELED, result);
        } else {
            result.putString("stream", "PASS: real Companion constructor, unique header ownership, measurement and drawing at two surface sizes; diagnostics FileProvider attachment readback and rejection of paths outside report cache. "+reportCheck+" This does not certify live gameplay or display transitions.");
            finish(Activity.RESULT_OK, result);
        }
    }
    private void diagnosticsSmoke(Context context) throws Exception {
        String authority=context.getPackageName()+".diagnostics";
        android.content.pm.ProviderInfo info=context.getPackageManager().resolveContentProvider(authority,0);
        if(info==null||info.exported||!info.grantUriPermissions) throw new AssertionError("Diagnostic provider must be private with temporary URI grants");
        Class<?> provider=context.getClassLoader().loadClass("androidx.core.content.FileProvider");
        Method uriFor=provider.getMethod("getUriForFile",Context.class,String.class,java.io.File.class);
        java.io.File dir=new java.io.File(context.getCacheDir(),"log-exports"); dir.mkdirs();
        java.io.File report=new java.io.File(dir,"provider-test.txt");
        java.io.File forbidden=new java.io.File(context.getCacheDir(),"forbidden-test.txt");
        try {
            if(requireReport){
                java.io.File real=new java.io.File(dir,"OpenFusion-Pocket-Logs.txt");
                if(!real.isFile()||real.length()>600000) throw new AssertionError("Exported real report missing or too large");
                byte[] data=new byte[(int)real.length()];
                try(java.io.DataInputStream input=new java.io.DataInputStream(new java.io.FileInputStream(real))){input.readFully(data);}
                String text=new String(data,"UTF-8");
                int first=text.indexOf("|Session|Started"),last=text.lastIndexOf("|Session|Started");
                if(!text.startsWith("OpenFusion:Pocket - Export Logs / schema 1")||!text.contains("GPU:")||!text.contains("PREVIOUS SESSION")||first<0||first==last)
                    throw new AssertionError("Actual report must include build/device metadata and distinct retained/current session starts");
                reportCheck="Actual exported report: "+data.length+" bytes; current and previous sessions retained across app restart.";
            }
            try(java.io.FileOutputStream out=new java.io.FileOutputStream(report)){out.write("synthetic-safe-report".getBytes("UTF-8"));}
            android.net.Uri uri=(android.net.Uri)uriFor.invoke(null,context,authority,report);
            if(!"content".equals(uri.getScheme())) throw new AssertionError("Share must use content URI");
            try(java.io.InputStream in=context.getContentResolver().openInputStream(uri)){
                byte[] bytes=new byte[64]; int count=in.read(bytes);
                if(!"synthetic-safe-report".equals(new String(bytes,0,count,"UTF-8"))) throw new AssertionError("Attachment readback mismatch");
            }
            boolean rejected=false;
            try{uriFor.invoke(null,context,authority,forbidden);}
            catch(java.lang.reflect.InvocationTargetException expected){rejected=expected.getCause() instanceof IllegalArgumentException;}
            if(!rejected) throw new AssertionError("Provider exposes files outside report cache");
        } finally { report.delete(); }
    }
}
