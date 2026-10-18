package com.openfusion.dualscreen;

import android.content.Context;
import android.graphics.*;
import android.view.MotionEvent;
import android.view.View;
import org.json.*;
import java.util.ArrayList;

/** Live presentation of InventoryWindow. Item actions remain in the original Unity dialogs. */
final class EquipmentPane extends View {
    interface Preview { boolean draw(Canvas canvas, RectF destination, Paint paint); }
    private final NanoPane.Images images;
    private final Preview preview;
    private final Paint p = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final Path path = new Path();
    private final float d;
    private JSONObject state;
    private boolean connected, restored;
    private float inventoryScroll, equipmentScroll, startX, startY, lastY, lastX, rotationDelta;
    private boolean dragging;
    private long lastRotation;
    private final RectF modelArea=new RectF();
    private int region;
    private final RectF inventoryArea = new RectF(), equipmentArea = new RectF();
    private final ArrayList<Hit> hits = new ArrayList<>();
    private static final class Hit {
        final RectF rect; final JSONObject item;
        Hit(RectF r, JSONObject i) { rect = new RectF(r); item = i; }
    }
    EquipmentPane(Context context, NanoPane.Images source, Preview animated) {
        super(context); images = source; preview = animated;
        d = context.getResources().getDisplayMetrics().density;
        setContentDescription("Equipment. Character statistics, equipped items and inventory. Tap an item for its original details and actions on the main screen.");
    }
    void update(JSONObject value, boolean online, int scroll) {
        state = value; connected = online;
        if (value!=null && !restored) { inventoryScroll=scroll; restored=true; }
        invalidate();
    }
    private void frame(Canvas c, RectF r) { PocketArt.frame(c,r,false,p,path,d); }
    private void text(Canvas c,String s,float x,float y,float width,float size,int color) { PocketArt.text(c,s,x,y,width,size*d,color,p); }
    private void image(Canvas c,String file,RectF r) {
        Bitmap bitmap=images.get(file); if(bitmap==null||bitmap.isRecycled()) return;
        float ratio=Math.min(r.width()/bitmap.getWidth(),r.height()/bitmap.getHeight());
        float w=bitmap.getWidth()*ratio,h=bitmap.getHeight()*ratio;
        p.setColor(Color.WHITE); p.setAlpha(255); c.drawBitmap(bitmap,null,new RectF(r.centerX()-w/2,r.centerY()-h/2,r.centerX()+w/2,r.centerY()+h/2),p);
    }
    private void bar(Canvas c,String label,int value,int max,String fill,float x,float y,float w) {
        text(c,label,x,y,w,9,PocketArt.CYAN);
        RectF r=new RectF(x,y+4*d,x+w,y+16*d);
        PocketArt.energy(c,r,value,max,true,p,d,images.get(fill),null);
        text(c,value+" / "+max,x+3*d,y+14*d,w-6*d,8,Color.WHITE);
    }
    protected void onDraw(Canvas c) {
        hits.clear(); if(state==null) { text(c,"Waiting for character dataâ€¦",8*d,28*d,getWidth()-16*d,12,Color.WHITE); return; }
        float w=getWidth(),h=getHeight(),footer=36*d;
        if(w<=0||h<=footer) return;
        float left=w*.45f, middle=w*.19f;
        RectF character=new RectF(2*d,2*d,left-3*d,h-3*d); frame(c,character);
        text(c,state.optString("name"),10*d,19*d,left-20*d,12,Color.WHITE);
        text(c,"LEVEL "+state.optInt("level"),10*d,35*d,left-20*d,10,PocketArt.CYAN);
        float barsY=h-79*d;
        modelArea.set(5*d,40*d,left*.64f,Math.max(45*d,barsY-3*d));
        RectF model=modelArea;
        if(!preview.draw(c,model,p)) text(c,"Loading avatar...",model.left,model.centerY(),model.width(),9,PocketArt.CYAN);
        float statX=left*.64f, statW=left-statX-6*d, statH=Math.max(24*d,Math.min(48*d,(barsY-45*d)/3));
        String[] titles={"ONE","AREA","DEFENSE"}, keys={"one","many","defense"}, icons={"oneImage","manyImage","defenseImage"};
        for(int i=0;i<3;i++) {
            float y=43*d+i*statH;
            image(c,state.optString(icons[i]),new RectF(statX,y,statX+22*d,y+22*d));
            text(c,titles[i],statX+24*d,y+9*d,statW-24*d,8,PocketArt.CYAN);
            text(c,""+state.optInt(keys[i]),statX+24*d,y+23*d,statW-24*d,12,Color.WHITE);
        }
        bar(c,"HEALTH",state.optInt("hp"),state.optInt("maxHp"),state.optString("hpFill"),10*d,barsY,left-20*d);
        bar(c,"FUSION MATTER",state.optInt("fusion"),state.optInt("maxFusion"),state.optString("fmFill"),10*d,barsY+29*d,left-20*d);
        image(c,state.optString("guideImage"),new RectF(8*d,h-27*d,30*d,h-5*d));
        text(c,"Guide: "+state.optString("guide"),34*d,h-9*d,left-42*d,9,Color.WHITE);
        // Both columns have independent touch scrolling; no rebuild resets their position.
        equipmentArea.set(left,22*d,left+middle-3*d,h-footer);
        inventoryArea.set(left+middle,22*d,w-2*d,h-footer);
        text(c,"EQUIPPED",equipmentArea.left+4*d,15*d,equipmentArea.width()-8*d,9,PocketArt.CYAN);
        text(c,"INVENTORY",inventoryArea.left+4*d,15*d,inventoryArea.width()-8*d,10,PocketArt.CYAN);
        JSONArray eq=state.optJSONArray("equipped"), inv=state.optJSONArray("inventory");
        float eqHeight=48*d;
        equipmentScroll=Math.max(0,Math.min(equipmentScroll,Math.max(0,(eq==null?0:eq.length())*eqHeight-equipmentArea.height())));
        c.save();c.clipRect(equipmentArea);
        if(eq!=null) for(int i=0;i<eq.length();i++) {
            JSONObject item=eq.optJSONObject(i); if(item==null)continue;
            float y=equipmentArea.top+i*eqHeight-equipmentScroll;
            RectF r=new RectF(equipmentArea.left,y,equipmentArea.right,y+eqHeight-2*d);
            if(!RectF.intersects(r,equipmentArea))continue;
            frame(c,r); image(c,item.optString("image"),new RectF(r.left+2*d,r.top+2*d,r.left+35*d,r.bottom-2*d));
            text(c,item.optString("label"),r.left+37*d,r.centerY()+3*d,r.width()-40*d,8,Color.WHITE);
            addHit(r,equipmentArea,item);
        } c.restore();
        int columns=Math.max(2,Math.min(4,(int)(inventoryArea.width()/(48*d))));
        float cell=inventoryArea.width()/columns;
        int count=inv==null?0:inv.length();
        inventoryScroll=Math.max(0,Math.min(inventoryScroll,Math.max(0,((count+columns-1)/columns)*cell-inventoryArea.height())));
        c.save();c.clipRect(inventoryArea);
        for(int i=0;i<count;i++) {
            JSONObject item=inv.optJSONObject(i); if(item==null)continue;
            float x=inventoryArea.left+i%columns*cell,y=inventoryArea.top+i/columns*cell-inventoryScroll;
            RectF r=new RectF(x+1*d,y+1*d,x+cell-2*d,y+cell-2*d);
            if(!RectF.intersects(r,inventoryArea))continue;
            frame(c,r); image(c,item.optString("image"),new RectF(r.left+4*d,r.top+4*d,r.right-4*d,r.bottom-4*d));
            if(item.optInt("quantity")>1)text(c,""+item.optInt("quantity"),r.left+3*d,r.top+12*d,r.width()-6*d,10,Color.WHITE);
            addHit(r,inventoryArea,item);
        }c.restore();
        String hint=!connected?"Disconnected":state.optBoolean("busy")?"Item details on main screen":"Tap an item for details / actions";
        text(c,hint,left+5*d,h-footer+13*d,w-left-10*d,8,PocketArt.CYAN);
        RectF more=new RectF(left+4*d,h-21*d,w-3*d,h-2*d);frame(c,more);
        text(c,"MY STUFF / MORE OPTIONS",more.left+6*d,more.bottom-5*d,more.width()-12*d,9,Color.WHITE);
    }
    private void addHit(RectF r,RectF clip,JSONObject item) { RectF hit=new RectF(r);if(hit.intersect(clip))hits.add(new Hit(hit,item)); }
    public boolean onTouchEvent(MotionEvent e) {
        float x=e.getX(),y=e.getY();
        switch(e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                startX=lastX=x;startY=lastY=y;rotationDelta=0;dragging=false;region=inventoryArea.contains(x,y)?1:equipmentArea.contains(x,y)?2:modelArea.contains(x,y)?3:0;return true;
            case MotionEvent.ACTION_MOVE:
                if(Math.abs(y-startY)>6*d||Math.abs(x-startX)>6*d)dragging=true;
                if(dragging&&(region==1||region==2)){if(region==1)inventoryScroll=Math.max(0,inventoryScroll+lastY-y);else equipmentScroll=Math.max(0,equipmentScroll+lastY-y);invalidate();}
                if(dragging&&region==3&&connected&&state!=null&&!state.optBoolean("busy")) {
                    rotationDelta+=(x-lastX)/d*.7f;
                    long now=android.os.SystemClock.uptimeMillis();if(now-lastRotation>=40){sendRotation();lastRotation=now;}
                }
                lastX=x;lastY=y;return true;
            case MotionEvent.ACTION_UP:
                if(region==3)sendRotation();
                if(region==1)PocketDualScreen.send("equipmentScroll","",0,inventoryScroll);
                if(!dragging&&state!=null&&connected&&!state.optBoolean("busy")) {
                    if(x>=getWidth()*.45f&&y>getHeight()-24*d)PocketDualScreen.send("window","Equipment",0,0);
                    else for(Hit hit:hits)if(hit.rect.contains(x,y)&&hit.item.optBoolean("inspectable")) {
                        JSONObject it=hit.item;
                        PocketDualScreen.send("itemInspect",it.optString("key"),it.optInt("slot"),it.optBoolean("equipped")?1:0);break;
                    }
                    performClick();
                }dragging=false;return true;
            case MotionEvent.ACTION_CANCEL:dragging=false;return true;
        }return true;
    }
    private void sendRotation(){if(rotationDelta!=0){PocketDualScreen.send("equipmentRotate","",rotationDelta,0);rotationDelta=0;}}
    public boolean performClick(){super.performClick();return true;}
}
