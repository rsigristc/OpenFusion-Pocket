package com.openfusion.dualscreen;

import android.content.Context;
import android.graphics.*;
import android.view.View;
import org.json.JSONObject;

/** Vector chrome around the client's real asset icons and live values. */
final class PocketArt {
    static final int INK=Color.rgb(0,22,35), CYAN=Color.rgb(55,224,249), LIME=Color.rgb(221,255,0);
    static void frame(Canvas c,RectF r,boolean selected,Paint p,Path path,float d) {
        float cut=7*d; path.reset(); path.moveTo(r.left+cut,r.top); path.lineTo(r.right-cut,r.top);
        path.lineTo(r.right,r.top+cut); path.lineTo(r.right,r.bottom-cut); path.lineTo(r.right-cut,r.bottom);
        path.lineTo(r.left+cut,r.bottom); path.lineTo(r.left,r.bottom-cut); path.lineTo(r.left,r.top+cut); path.close();
        p.setStyle(Paint.Style.FILL); p.setShader(new LinearGradient(0,r.top,0,r.bottom,Color.rgb(7,55,77),INK,Shader.TileMode.CLAMP)); c.drawPath(path,p); p.setShader(null);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth((selected?2:1)*d); p.setColor(selected?LIME:CYAN); c.drawPath(path,p);
        p.setStrokeWidth(d); p.setColor(selected?LIME:Color.rgb(22,96,116)); c.drawLine(r.left+cut,r.top+3*d,r.right-cut,r.top+3*d,p); p.setStyle(Paint.Style.FILL);
    }
    static void text(Canvas c,String s,float x,float y,float width,float size,int color,Paint p) {
        p.setColor(color); p.setTypeface(Typeface.create(Typeface.SANS_SERIF,Typeface.BOLD)); p.setTextSize(size);
        if(p.measureText(s)>width) p.setTextSize(Math.max(size*.7f,size*width/Math.max(1,p.measureText(s))));
        c.save(); c.clipRect(x,y-size,width+x,y+size*.3f); c.drawText(s,x,y,p); c.restore();
    }
    static int nanoStyleColor(int style) { return style==1?Color.rgb(255,76,92):style==2?LIME:CYAN; }
    static void energy(Canvas c,RectF r,int value,int max,boolean known,Paint p,float d,Bitmap fill,Bitmap frame) {
        p.setColor(Color.WHITE); p.setAlpha(255); p.setStyle(Paint.Style.FILL);
        if(frame!=null&&!frame.isRecycled()) c.drawBitmap(frame,null,r,p);
        else { p.setColor(Color.rgb(0,9,21)); c.drawRect(r,p); p.setColor(CYAN); p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(d); c.drawRect(r,p); p.setStyle(Paint.Style.FILL); }
        if(known && max>0) {
            float left=r.left,top=r.top,right=r.right,bottom=r.bottom;
            r.inset(d,d); r.right=r.left+Math.max(0,r.width())*Math.max(0,Math.min(1,(float)value/max));
            // Like Legacy: compress the entire HP_BAR gradient into the remaining stamina.
            if(fill!=null&&!fill.isRecycled()) { p.setColor(Color.WHITE); c.drawBitmap(fill,null,r,p); }
            else { p.setColor(LIME); c.drawRect(r,p); }
            r.set(left,top,right,bottom);
        }
    }
}

final class NanoCardView extends View {
    private final NanoPane.Images images;
    private final NanoPane.Previews previews;
    private final boolean equipped;
    private JSONObject nano;
    private int slot;
    private int activeSlot=-1;
    private boolean highlighted;
    private String energyFill="",energyFrame="",skillBack="";
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Path path=new Path(); private final RectF box=new RectF();
    private final float d;
    NanoCardView(Context c,NanoPane.Images source,NanoPane.Previews animated,boolean large) { super(c); images=source; previews=animated; equipped=large; d=c.getResources().getDisplayMetrics().density; setClickable(true); }
    void setArt(JSONObject art) { energyFill=art.optString("energyFill"); energyFrame=art.optString("energyFrame"); skillBack=art.optString("skillBack"); }
    void update(JSONObject value,int index,boolean selected) {
        nano=value; slot=index; highlighted=selected;
        describe(); invalidate();
    }
    private boolean summoned() { return nano!=null && activeSlot>=0 && nano.optInt("slot",-1)==activeSlot; }
    void setActiveSlot(int value) {
        boolean was=summoned(); activeSlot=value;
        if(was!=summoned()) { describe(); invalidate(); }
    }
    private void describe() {
        setContentDescription(nano==null?"Empty Nano slot "+(slot+1):nano.optString("name")+(nano.optBoolean("owned")?", owned":", locked")+
            (slot>=0?", equipped slot "+(slot+1):"")+(summoned()?", summoned":""));
    }
    protected void onDraw(Canvas c) {
        float w=getWidth(),h=getHeight(); box.set(2*d,2*d,w-2*d,h-2*d); PocketArt.frame(c,box,highlighted,paint,path,d);
        if(nano==null) { PocketArt.text(c,"EMPTY",8*d,h*.55f,w-16*d,11*d,PocketArt.CYAN,paint); return; }
        boolean owned=nano.optBoolean("owned");
        Bitmap b=images.get(nano.optString("image"));
        float bottom=h-(equipped?43:20)*d, side=Math.max(1,Math.min(w-8*d,bottom-7*d));
        box.set((w-side)/2,7*d,(w+side)/2,7*d+side);
        boolean animated=owned && equipped && previews.draw(c,nano.optInt("id"),box,paint);
        if(!animated && b!=null&&!b.isRecycled()) { paint.setAlpha(owned?255:150); c.drawBitmap(b,null,box,paint); paint.setAlpha(255); }
        if(slot>=0) { paint.setColor(PocketArt.CYAN); box.set(4*d,4*d,22*d,22*d); c.drawRoundRect(box,3*d,3*d,paint); PocketArt.text(c,Integer.toString(slot+1),8*d,18*d,12*d,12*d,PocketArt.INK,paint); }
        if(summoned()) { paint.setColor(PocketArt.LIME); c.drawCircle(w-12*d,12*d,4*d,paint); }
        PocketArt.text(c,owned?nano.optString("name").toUpperCase(java.util.Locale.US):"LOCKED",7*d,h-(equipped?30:7)*d,w-14*d,10*d,owned?Color.WHITE:Color.LTGRAY,paint);
        if(equipped) {
            int color=PocketArt.nanoStyleColor(nano.optInt("styleIndex"));
            PocketArt.text(c,nano.optString("style").toUpperCase(java.util.Locale.US),7*d,h-18*d,w-38*d,9*d,color,paint);
            box.set(8*d,h-12*d,Math.max(9*d,w-33*d),h-5*d); PocketArt.energy(c,box,nano.optInt("energy"),nano.optInt("max"),nano.optBoolean("energyKnown"),paint,d,images.get(energyFill),images.get(energyFrame));
            Bitmap skill=images.get(nano.optString("skillImage"));
            if(skill!=null&&!skill.isRecycled()) {
                box.set(w-29*d,h-29*d,w-5*d,h-5*d); Bitmap back=images.get(skillBack); paint.setColor(Color.WHITE); paint.setAlpha(255);
                if(back!=null&&!back.isRecycled()) c.drawBitmap(back,null,box,paint);
                box.inset(2*d,2*d); c.drawBitmap(skill,null,box,paint);
            }
        }
    }
}

/** Touch-sized tabs with the angular silhouette and lit selection of the reference. */
final class MapTab extends View {
    private final String label;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); private final Path path=new Path(); private final RectF r=new RectF(); private final float d;
    MapTab(Context c,String title) { super(c); label=title; d=c.getResources().getDisplayMetrics().density; setClickable(true); setContentDescription(title+" map"); }
    protected void drawableStateChanged() { super.drawableStateChanged(); invalidate(); }
    protected void onDraw(Canvas c) {
        boolean lit=isSelected()||isPressed(); r.set(2*d,5*d,getWidth()-2*d,getHeight()-5*d); PocketArt.frame(c,r,lit,p,path,d);
        if(lit) {
            p.setStyle(Paint.Style.FILL); p.setShader(new LinearGradient(0,r.top,0,r.bottom,Color.rgb(246,255,80),Color.rgb(171,219,0),Shader.TileMode.CLAMP)); c.drawPath(path,p); p.setShader(null);
            p.setColor(Color.argb(120,235,255,0)); p.setStrokeWidth(4*d); p.setStyle(Paint.Style.STROKE); c.drawPath(path,p); p.setStyle(Paint.Style.FILL);
        }
        p.setTypeface(Typeface.create(Typeface.SANS_SERIF,Typeface.BOLD)); p.setTextSize(14*d); p.setColor(lit?PocketArt.INK:PocketArt.CYAN);
        c.drawText(label,(getWidth()-p.measureText(label))/2,(getHeight()-p.ascent()-p.descent())/2,p);
    }
}

final class MapZoomButton extends View {
    private final boolean plus; private final float d; private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
    MapZoomButton(Context c,boolean increase) { super(c); plus=increase; d=c.getResources().getDisplayMetrics().density; setClickable(true); setContentDescription(increase?"Zoom in":"Zoom out"); }
    protected void drawableStateChanged() { super.drawableStateChanged(); invalidate(); }
    protected void onDraw(Canvas c) {
        float x=getWidth()/2f,y=getHeight()/2f,r=Math.min(getWidth(),getHeight())/2f-8*d;
        int color=isEnabled()?PocketArt.LIME:Color.rgb(82,103,36);
        p.setStyle(Paint.Style.FILL); p.setShader(new RadialGradient(x,y,r,new int[]{Color.rgb(94,110,0),Color.rgb(11,25,6)},null,Shader.TileMode.CLAMP)); c.drawCircle(x,y,r,p); p.setShader(null);
        p.setStyle(Paint.Style.STROKE); p.setStrokeWidth((isPressed()?3:2)*d); p.setColor(color); c.drawCircle(x,y,r,p);
        p.setColor(isEnabled()?Color.WHITE:Color.GRAY); p.setStrokeWidth(3*d); p.setStrokeCap(Paint.Cap.ROUND); c.drawLine(x-7*d,y,x+7*d,y,p);
        if(plus) c.drawLine(x,y-7*d,x,y+7*d,p); p.setStrokeCap(Paint.Cap.BUTT); p.setStyle(Paint.Style.FILL);
    }
}

final class MapZoomTicks extends View {
    private int level; private final float d; private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); private final RectF r=new RectF();
    MapZoomTicks(Context c) { super(c); d=c.getResources().getDisplayMetrics().density; setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO); }
    void update(boolean canIn,boolean canOut) { int next=canIn?(canOut?2:1):(canOut?3:0); if(level!=next) { level=next; invalidate(); } }
    protected void onDraw(Canvas c) {
        for(int i=0;i<3;i++) { p.setColor(level>i?PocketArt.LIME:Color.rgb(61,77,22)); r.set(getWidth()/2-8*d,(3+i*8)*d,getWidth()/2+8*d,(8+i*8)*d); c.drawRoundRect(r,2*d,2*d,p); }
    }
}

final class MapLocation extends android.widget.TextView {
    private String place=""; private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); private final Path path=new Path(); private final float d;
    MapLocation(Context c) { super(c); d=c.getResources().getDisplayMetrics().density; setTextSize(12); setTextColor(Color.WHITE); setPadding((int)(10*d),0,0,0); setGravity(android.view.Gravity.CENTER_VERTICAL); setSingleLine(true); }
    void setLocation(String value) { place=value; setText("Location: "+value); }
    protected void onDraw(Canvas c) {
        p.setTypeface(Typeface.create(Typeface.SANS_SERIF,Typeface.BOLD)); p.setTextSize(12*d);
        float edge=Math.min(getWidth()-2*d,p.measureText(getText().toString())+28*d),h=getHeight();
        path.reset(); path.moveTo(1,0); path.lineTo(edge-10*d,0); path.lineTo(edge,h-1); path.lineTo(7*d,h-1); path.lineTo(1,h-7*d); path.close();
        p.setColor(PocketArt.INK); p.setStyle(Paint.Style.FILL); c.drawPath(path,p); p.setColor(PocketArt.CYAN); p.setStrokeWidth(d); p.setStyle(Paint.Style.STROKE); c.drawPath(path,p); c.drawLine(edge,h-1,getWidth()-1,h-1,p); p.setStyle(Paint.Style.FILL);
        if(place.isEmpty()||!getText().toString().equals("Location: "+place)) { super.onDraw(c); return; }
        float baseline=(h-p.ascent()-p.descent())/2; p.setColor(Color.WHITE); c.drawText("Location: ",10*d,baseline,p);
        float prefix=p.measureText("Location: "); PocketArt.text(c,place,10*d+prefix,baseline,getWidth()-prefix-25*d,12*d,PocketArt.LIME,p);
    }
}

final class MapPage extends android.widget.LinearLayout {
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); private final Path path=new Path(); private final float d;
    MapPage(Context c) { super(c); d=c.getResources().getDisplayMetrics().density; }
    protected void dispatchDraw(Canvas c) {
        super.dispatchDraw(c); float w=getWidth(),h=getHeight(),cut=7*d;
        path.reset(); path.moveTo(cut,1); path.lineTo(w-cut,1); path.lineTo(w-1,cut); path.lineTo(w-1,h-cut); path.lineTo(w-cut,h-1); path.lineTo(cut,h-1); path.lineTo(1,h-cut); path.lineTo(1,cut); path.close();
        p.setColor(PocketArt.CYAN); p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(d); c.drawPath(path,p);
    }
}

final class BoostStrip extends View {
    private int nano,attack,taros; private boolean connected, connectionKnown;
    private String coin=""; private final NanoPane.Images images;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG); private final Path path=new Path(); private final RectF r=new RectF();
    private final float d;
    BoostStrip(Context c,NanoPane.Images source) { super(c); images=source; d=c.getResources().getDisplayMetrics().density; }
    void update(JSONObject data) { nano=data.optInt("nanoBoost"); attack=data.optInt("attackBoost"); taros=data.optInt("taros"); coin=data.optString("tarosImage"); connectionKnown=data.has("connected"); connected=data.optBoolean("connected"); setContentDescription(connected?"Nano boost "+nano+", attack boost "+attack+", Taros "+taros:connectionKnown?"Game server disconnected. Return to the launcher to reconnect.":"Waiting for live game data…"); invalidate(); }
    protected void onDraw(Canvas c) {
        if(!connected) { PocketArt.text(c,connectionKnown?"Game server disconnected. Return to the launcher to reconnect.":"Waiting for live game data…",8*d,25*d,getWidth()-16*d,13*d,Color.YELLOW,p); return; }
        float cell=Math.min(getWidth()/3f,235*d),h=getHeight();
        for(int i=0;i<3;i++) {
            float x=i*cell; r.set(x+2*d,2*d,x+cell-4*d,h-2*d); PocketArt.frame(c,r,false,p,path,d);
            p.setColor(PocketArt.CYAN); p.setStrokeWidth(2*d); p.setStyle(Paint.Style.STROKE);
            if(i==0) { c.drawCircle(x+21*d,h-16*d,9*d,p); c.drawLine(x+18*d,h-25*d,x+18*d,9*d,p); c.drawLine(x+24*d,h-25*d,x+24*d,9*d,p); c.drawLine(x+17*d,9*d,x+25*d,9*d,p); }
            else if(i==1) { p.setStyle(Paint.Style.FILL); path.reset(); path.moveTo(x+22*d,7*d); path.lineTo(x+12*d,24*d); path.lineTo(x+20*d,24*d); path.lineTo(x+16*d,h-6*d); path.lineTo(x+31*d,18*d); path.lineTo(x+23*d,18*d); path.close(); c.drawPath(path,p); }
            else {
                Bitmap icon=images.get(coin); p.setStyle(Paint.Style.FILL);
                if(icon!=null&&!icon.isRecycled()) { r.set(x+8*d,9*d,x+33*d,h-9*d); c.drawBitmap(icon,null,r,p); }
                else { p.setStyle(Paint.Style.STROKE); for(int j=0;j<3;j++) { r.set(x+10*d,(12+j*5)*d,x+31*d,(20+j*5)*d); c.drawOval(r,p); } }
            }
            p.setStyle(Paint.Style.FILL); PocketArt.text(c,i==0?"NANO BOOST":i==1?"ATTACK BOOST":"TAROS",x+39*d,16*d,cell-48*d,10*d,Color.WHITE,p);
            PocketArt.text(c,Integer.toString(i==0?nano:i==1?attack:taros),x+39*d,h-8*d,cell-48*d,17*d,Color.WHITE,p);
        }
    }
}

/** Original map sprites with the same server-independent preference mask as Unity. */
final class MapFilterTile extends View {
    int id; private JSONObject data; private final NanoPane.Images images;
    private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private final Path path=new Path(); private final RectF r=new RectF(); private final float d;
    MapFilterTile(Context c,NanoPane.Images source) { super(c); images=source; d=c.getResources().getDisplayMetrics().density; setClickable(true); }
    void update(JSONObject value,boolean enabled) {
        data=value; id=value.optInt("id"); setEnabled(enabled); setSelected(value.optBoolean("selected"));
        setContentDescription(value.optString("label")+(enabled?(isSelected()?", shown":", hidden"):", all categories shown in Local view")); invalidate();
    }
    protected void onDraw(Canvas c) {
        r.set(3*d,5*d,getWidth()-3*d,getHeight()-5*d); PocketArt.frame(c,r,isEnabled()&&isSelected(),p,path,d);
        if(data==null) return; Bitmap icon=images.get(data.optString("image")); p.setAlpha(isEnabled()&&!isSelected()?95:255);
        if(icon!=null&&!icon.isRecycled()) {
            float side=26*d,ratio=(float)icon.getWidth()/icon.getHeight(),w=side*Math.min(1,ratio),h=side/Math.max(1,ratio);
            r.set((getWidth()-w)/2,(getHeight()-h)/2,(getWidth()+w)/2,(getHeight()+h)/2); c.drawBitmap(icon,null,r,p);
        } else PocketArt.text(c,Integer.toString(id),12*d,getHeight()/2+4*d,getWidth()-20*d,11*d,PocketArt.CYAN,p);
        p.setAlpha(255);
    }
}

final class NavigationTile extends View {
    private final String label; private final int icon;
    private final float d; private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG); private final Path path=new Path(); private final RectF r=new RectF();
    NavigationTile(Context c,String name,int index) { super(c); label=name; icon=index; d=c.getResources().getDisplayMetrics().density; setClickable(true); setContentDescription(name); }
    protected void onDraw(Canvas c) {
        float w=getWidth(),h=getHeight(),cx=w/2,cy=16*d; r.set(2*d,2*d,w-2*d,h-2*d); PocketArt.frame(c,r,isSelected()||isPressed(),p,path,d);
        if(isSelected()) { p.setColor(PocketArt.LIME); c.drawPath(path,p); }
        p.setColor(!isEnabled()?Color.rgb(79,110,121):isSelected()?PocketArt.INK:PocketArt.CYAN); p.setStyle(Paint.Style.STROKE); p.setStrokeWidth(2*d);
        if(icon==0) { c.drawCircle(cx,cy-2*d,6*d,p); c.drawLine(cx-5*d,cy+2*d,cx,cy+11*d,p); c.drawLine(cx+5*d,cy+2*d,cx,cy+11*d,p); }
        else if(icon==1) { c.drawLine(cx,cy-5*d,cx-9*d,cy+7*d,p); c.drawLine(cx,cy-5*d,cx+9*d,cy+7*d,p); c.drawCircle(cx,cy-7*d,3*d,p); c.drawCircle(cx-9*d,cy+7*d,3*d,p); c.drawCircle(cx+9*d,cy+7*d,3*d,p); }
        else if(icon==2) { r.set(cx-9*d,cy-5*d,cx+9*d,cy+10*d); c.drawRoundRect(r,3*d,3*d,p); r.set(cx-4*d,cy-10*d,cx+4*d,cy-5*d); c.drawRect(r,p); c.drawLine(cx-5*d,cy+3*d,cx+5*d,cy+3*d,p); }
        else if(icon==3) { r.set(cx-8*d,cy-9*d,cx+8*d,cy+10*d); c.drawRect(r,p); for(int j=0;j<3;j++) c.drawLine(cx-5*d,cy+(-3+j*4)*d,cx+5*d,cy+(-3+j*4)*d,p); }
        else { r.set(cx-10*d,cy-7*d,cx+10*d,cy+7*d); c.drawRoundRect(r,3*d,3*d,p); if(icon==4) { c.drawLine(cx-5*d,cy+7*d,cx-8*d,cy+12*d,p); c.drawCircle(cx-4*d,cy,1*d,p); c.drawCircle(cx+4*d,cy,1*d,p); } else { c.drawLine(cx-9*d,cy-6*d,cx,cy+1*d,p); c.drawLine(cx+9*d,cy-6*d,cx,cy+1*d,p); } }
        p.setStyle(Paint.Style.FILL);
        String title=label.toUpperCase(java.util.Locale.US); float size=9*d;
        p.setTypeface(Typeface.create(Typeface.SANS_SERIF,Typeface.BOLD)); p.setTextSize(size);
        if(p.measureText(title)>w-12*d) p.setTextSize(Math.max(size*.7f,size*(w-12*d)/Math.max(1,p.measureText(title))));
        p.setColor(!isEnabled()?Color.GRAY:isSelected()?PocketArt.INK:Color.WHITE);
        c.save(); c.clipRect(6*d,h-20*d,w-6*d,h); c.drawText(title,(w-p.measureText(title))/2,h-8*d,p); c.restore();
    }
}
