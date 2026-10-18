package com.openfusion.dualscreen;

import android.app.AlertDialog;
import android.content.Context;
import android.graphics.*;
import android.view.View;
import android.view.ViewGroup;
import android.widget.*;
import org.json.*;

/** Journal presentation only. Mission state, progress, tracking and packets stay in Unity. */
final class MissionPane extends LinearLayout {
    private final NanoPane.Images images;
    private final ScrollView listScroll, detailScroll;
    private final LinearLayout list, details;
    private final Button active, completed, follow, abandon;
    private final float density;
    private JSONObject state, selected;
    private boolean online, applyingScroll, restored, released;
    private String listKey="", detailKey="", identity="";
    private AlertDialog confirmation;
    private final Runnable scrollChanged=this::sendScroll;
    MissionPane(Context context,NanoPane.Images source) {
        super(context); images=source; density=context.getResources().getDisplayMetrics().density;
        setOrientation(HORIZONTAL);
        LinearLayout left=new LinearLayout(context);left.setOrientation(VERTICAL);PanelSkin.cell(left,false);
        LayoutParams leftParams=new LayoutParams(0,-1,42);leftParams.setMargins(0,dp(3),dp(4),dp(3));addView(left,leftParams);
        LinearLayout tabs=new LinearLayout(context);left.addView(tabs,new LayoutParams(-1,dp(44)));
        active=button("ACTIVE");completed=button("COMPLETED");tabs.addView(active,new LayoutParams(0,-1,1));tabs.addView(completed,new LayoutParams(0,-1,1));
        active.setOnClickListener(v->PocketDualScreen.send("missionMode","0",0,0));completed.setOnClickListener(v->PocketDualScreen.send("missionMode","1",0,0));
        listScroll=new ScrollView(context);listScroll.setFillViewport(true);left.addView(listScroll,new LayoutParams(-1,0,1));
        list=column();listScroll.addView(list,new ScrollView.LayoutParams(-1,-2));
        LinearLayout right=column();PanelSkin.cell(right,false);LayoutParams rightParams=new LayoutParams(0,-1,58);rightParams.setMargins(0,dp(3),0,dp(3));addView(right,rightParams);
        detailScroll=new ScrollView(context);detailScroll.setFillViewport(true);right.addView(detailScroll,new LayoutParams(-1,0,1));
        details=column();details.setPadding(dp(5),dp(5),dp(5),dp(5));detailScroll.addView(details,new ScrollView.LayoutParams(-1,-2));
        LinearLayout actions=new LinearLayout(context);right.addView(actions,new LayoutParams(-1,dp(46)));
        follow=button("TRACK");abandon=button("ABANDON");actions.addView(follow,new LayoutParams(0,-1,1));actions.addView(abandon,new LayoutParams(0,-1,1));
        follow.setOnClickListener(v->{if(selected!=null)PocketDualScreen.send("missionFollow",Integer.toString(selected.optInt("id")),selected.optInt("task"),0);});
        abandon.setOnClickListener(v->confirmAbandon());
        listScroll.setOnScrollChangeListener((v,x,y,oldX,oldY)->queueScroll());detailScroll.setOnScrollChangeListener((v,x,y,oldX,oldY)->queueScroll());
        setContentDescription("Mission journal. Active and completed missions, real objectives and rewards.");
    }
    private int dp(int value){return Math.round(value*density);}
    private LinearLayout column(){LinearLayout layout=new LinearLayout(getContext());layout.setOrientation(VERTICAL);return layout;}
    private TextView text(String value,int size,int color){TextView label=new TextView(getContext());label.setText(value);label.setTextSize(size);label.setTextColor(color);label.setPadding(dp(4),dp(3),dp(4),dp(3));return label;}
    private Button button(String name){Button button=new Button(getContext());button.setText(name);button.setTextSize(10);button.setPadding(dp(2),0,dp(2),0);button.setMinWidth(0);button.setMinimumWidth(0);PanelSkin.button(button);return button;}
    private void queueScroll(){if(applyingScroll||state==null||released)return;removeCallbacks(scrollChanged);postDelayed(scrollChanged,120);}
    private void sendScroll(){if(state!=null&&!released)PocketDualScreen.send("missionScroll",state.optBoolean("completed")?"1":"0",listScroll.getScrollY(),detailScroll.getScrollY());}
    private void restore(ScrollView view,int y){view.post(()->{if(released)return;applyingScroll=true;view.scrollTo(0,y);applyingScroll=false;});}
    void update(JSONObject value,boolean connected){
        if(value==null||released)return;
        boolean modeChanged=state!=null&&state.optBoolean("completed")!=value.optBoolean("completed");
        state=value;online=connected;selected=state.optJSONObject("detail");boolean done=state.optBoolean("completed");
        active.setSelected(!done);completed.setSelected(done);
        follow.setVisibility(done||selected==null?GONE:VISIBLE);abandon.setVisibility(done||selected==null?GONE:VISIBLE);
        follow.setText(selected!=null&&selected.optBoolean("current")?"TRACKING":"TRACK");
        follow.setEnabled(online&&selected!=null&&selected.optBoolean("canFollow"));abandon.setEnabled(online&&selected!=null&&selected.optBoolean("canAbandon"));
        JSONArray entries=state.optJSONArray("entries");String nextList=done+":"+(selected==null?0:selected.optInt("id"))+":"+(entries==null?"":entries.toString());
        if(!nextList.equals(listKey)){
            int y=!restored||modeChanged?state.optInt("listScroll"):listScroll.getScrollY();listKey=nextList;rebuildList(entries,done);restore(listScroll,y);
        }
        String nextIdentity=selected==null?"none":selected.optInt("id")+":"+selected.optInt("task")+":"+done;
        String nextDetail=done+":"+(selected==null?"":selected.toString());
        if(!nextDetail.equals(detailKey)){
            int y=!restored||modeChanged?state.optInt("detailScroll"):nextIdentity.equals(identity)?detailScroll.getScrollY():0;
            detailKey=nextDetail;identity=nextIdentity;rebuildDetails(done);restore(detailScroll,y);
        }
        restored=true;
    }
    private void rebuildList(JSONArray entries,boolean done){
        list.removeAllViews();int[] types={2,1,3};String[] labels={"NANO","GUIDE","WORLD"},icons={"nanoIcon","guideIcon","worldIcon"};int total=0;
        for(int category=0;category<3;category++){
            int count=0;if(entries!=null)for(int i=0;i<entries.length();i++)if(entries.optJSONObject(i)!=null&&entries.optJSONObject(i).optInt("category")==types[category])count++;
            total+=count;
            LinearLayout header=new LinearLayout(getContext());header.setGravity(android.view.Gravity.CENTER_VERTICAL);list.addView(header,new LayoutParams(-1,dp(34)));
            header.addView(new Icon(state.optString(icons[category])),new LayoutParams(dp(28),dp(28)));
            TextView title=text(labels[category]+"  "+count+(done?"":"/"+(types[category]==3?4:1)),11,PocketArt.CYAN);title.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);header.addView(title,new LayoutParams(0,-2,1));
            if(entries==null)continue;
            for(int i=0;i<entries.length();i++){
                JSONObject entry=entries.optJSONObject(i);if(entry==null||entry.optInt("category")!=types[category])continue;
                final int id=entry.optInt("id"),task=entry.optInt("task");boolean highlighted=selected!=null&&selected.optInt("id")==id;
                LinearLayout row=new LinearLayout(getContext());row.setGravity(android.view.Gravity.CENTER_VERTICAL);row.setMinimumHeight(dp(60));PanelSkin.cell(row,highlighted);
                LayoutParams rowParams=new LayoutParams(-1,-2);rowParams.setMargins(dp(3),dp(2),dp(3),dp(2));list.addView(row,rowParams);
                row.addView(new Status(entry.optBoolean("ready"),entry.optBoolean("current"),highlighted),new LayoutParams(dp(28),dp(34)));
                LinearLayout words=column();row.addView(words,new LayoutParams(0,-2,1));int ink=highlighted?PocketArt.INK:Color.WHITE;
                TextView name=text(entry.optString("name"),11,ink);name.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);words.addView(name);
                words.addView(text(entry.optString("objective"),10,highlighted?PocketArt.INK:PocketArt.CYAN));
                row.setContentDescription(entry.optString("name")+(entry.optBoolean("current")?", tracked":"")+", "+entry.optString("objective"));row.setOnClickListener(v->PocketDualScreen.send("missionSelect",Integer.toString(id),task,0));
            }
        }
        if(total==0)list.addView(text(done?"No completed missions.":"No active missions.",12,Color.WHITE));
    }
    private void caption(String name){TextView label=text(name,10,PocketArt.INK);label.setBackgroundColor(PocketArt.CYAN);label.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);details.addView(label,new LayoutParams(-1,-2));}
    private void rebuildDetails(boolean done){
        details.removeAllViews();if(selected==null){details.addView(text("Select a mission to see its details.",13,Color.WHITE));return;}
        FrameLayout heading=new FrameLayout(getContext());heading.setMinimumHeight(dp(42));heading.addView(new Icon(selected.optString("banner"),true),new FrameLayout.LayoutParams(-1,-1));
        TextView title=text(selected.optString("name"),14,Color.WHITE);title.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);heading.addView(title,new FrameLayout.LayoutParams(-1,-2));details.addView(heading,new LayoutParams(-1,-2));
        LinearLayout npc=new LinearLayout(getContext());npc.setGravity(android.view.Gravity.CENTER_VERTICAL);details.addView(npc,new LayoutParams(-1,-2));
        npc.addView(new Icon(selected.optString("npcImage")),new LayoutParams(dp(50),dp(50)));LinearLayout who=column();npc.addView(who,new LayoutParams(0,-2,1));
        who.addView(text(selected.optString("npc"),12,PocketArt.LIME));who.addView(text(selected.optString("difficulty"),10,PocketArt.CYAN));who.addView(text(selected.optString("location"),10,Color.WHITE));
        caption(done?"MISSION SUMMARY":"MISSION DETAILS");
        if(!done&&!selected.optString("progress").isEmpty())details.addView(text(selected.optString("progress"),11,PocketArt.LIME));
        if(!selected.optString("description").isEmpty())details.addView(text(selected.optString("description"),11,Color.WHITE));
        if(!selected.optString("objective").isEmpty())details.addView(text(selected.optString("objective"),11,PocketArt.CYAN));
        caption("REWARDS");
        if(!selected.optString("nanoImage").isEmpty())reward(selected.optString("nanoImage"),selected.optString("nanoName"));
        JSONArray rewards=selected.optJSONArray("rewards");int lastGroup=-1;
        if(rewards!=null)for(int i=0;i<rewards.length();i++){
            JSONObject item=rewards.optJSONObject(i);if(item==null)continue;
            if(item.optInt("group")!=lastGroup){lastGroup=item.optInt("group");details.addView(text(item.optInt("choices")>0?"Choose up to "+item.optInt("choices")+" at mission turn-in:":"You will receive:",10,PocketArt.CYAN));}
            reward(item.optString("image"),item.optString("name"));
        }
        if(selected.optInt("fusion")>0)reward(state.optString("fusionIcon"),selected.optInt("fusion")+" Fusion Matter");
        if(selected.optInt("taros")>0)reward(state.optString("tarosIcon"),selected.optInt("taros")+" Taros");
        details.addView(text("Accept missions and claim rewards from the appropriate NPC.",9,PocketArt.CYAN));
    }
    private void reward(String image,String label){LinearLayout row=new LinearLayout(getContext());row.setGravity(android.view.Gravity.CENTER_VERTICAL);PanelSkin.cell(row,false);LayoutParams lp=new LayoutParams(-1,-2);lp.setMargins(0,dp(3),0,0);details.addView(row,lp);row.addView(new Icon(image),new LayoutParams(dp(36),dp(36)));row.addView(text(label,11,Color.WHITE),new LayoutParams(0,-2,1));}
    private void confirmAbandon(){
        if(selected==null||!online||!selected.optBoolean("canAbandon")||confirmation!=null)return;
        final int id=selected.optInt("id"),task=selected.optInt("task");
        confirmation=new AlertDialog.Builder(getContext()).setTitle("Abandon mission?").setMessage(selected.optString("name")+"\n\nThis removes the mission from your journal. You can obtain it again from its mission giver, according to the server's rules.")
            .setNegativeButton("Cancel",(dialog,which)->{}).setPositiveButton("Abandon",(dialog,which)->{if(!released)PocketDualScreen.send("missionAbandon",Integer.toString(id),task,0);}).create();
        confirmation.setOnDismissListener(dialog->confirmation=null);confirmation.show();
    }
    void invalidateImages(){invalidateTree(list);invalidateTree(details);}
    private void invalidateTree(ViewGroup group){for(int i=0;i<group.getChildCount();i++){View child=group.getChildAt(i);child.invalidate();if(child instanceof ViewGroup)invalidateTree((ViewGroup)child);}}
    void release(){released=true;removeCallbacks(scrollChanged);if(confirmation!=null)confirmation.dismiss();}
    protected void onDetachedFromWindow(){removeCallbacks(scrollChanged);if(confirmation!=null)confirmation.dismiss();super.onDetachedFromWindow();}
    private final class Icon extends View {
        final String file;final boolean stretch;final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
        Icon(String path){this(path,false);}Icon(String path,boolean fill){super(MissionPane.this.getContext());file=path;stretch=fill;}
        protected void onDraw(Canvas canvas){Bitmap image=images.get(file);if(image==null||image.isRecycled())return;float w=getWidth(),h=getHeight();if(stretch){p.setAlpha(150);canvas.drawBitmap(image,null,new RectF(0,0,w,h),p);return;}float scale=Math.min(w/image.getWidth(),h/image.getHeight());float iw=image.getWidth()*scale,ih=image.getHeight()*scale;canvas.drawBitmap(image,null,new RectF((w-iw)/2,(h-ih)/2,(w+iw)/2,(h+ih)/2),p);}
    }
    private final class Status extends View {
        final boolean ready,current,selected;final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);
        Status(boolean r,boolean c,boolean s){super(MissionPane.this.getContext());ready=r;current=c;selected=s;}
        protected void onDraw(Canvas canvas){float x=getWidth()/2f,y=getHeight()/2f,r=dp(8);p.setColor(selected?PocketArt.INK:current?PocketArt.LIME:PocketArt.CYAN);p.setStrokeWidth(dp(2));p.setStyle(Paint.Style.STROKE);canvas.drawCircle(x,y,r,p);if(ready){canvas.drawLine(x-r*.6f,y,x-r*.1f,y+r*.4f,p);canvas.drawLine(x-r*.1f,y+r*.4f,x+r*.7f,y-r*.6f,p);}}
    }
}
