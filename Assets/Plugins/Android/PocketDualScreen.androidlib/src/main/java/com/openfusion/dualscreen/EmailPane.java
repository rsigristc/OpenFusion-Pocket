package com.openfusion.dualscreen;

import android.app.AlertDialog;
import android.content.Context;
import android.graphics.*;
import android.view.*;
import android.widget.*;
import org.json.*;

/** Mail presentation only; Unity owns the shared Legacy mail model and all requests. */
final class EmailPane extends LinearLayout {
    private final NanoPane.Images images;
    private final LinearLayout list,details;
    private final ScrollView listScroll,detailScroll;
    private final Button guide,player,previous,next,claimItems,claimTaros,reply,delete,dismiss;
    private final TextView heading,status,page;
    private final float density;
    private JSONObject selected;
    private String listKey="",detailKey="",identity="";
    private boolean released,ready;
    private AlertDialog confirmation;
    EmailPane(Context c,NanoPane.Images source){
        super(c);images=source;density=c.getResources().getDisplayMetrics().density;setOrientation(VERTICAL);
        LinearLayout top=new LinearLayout(c);addView(top,new LayoutParams(-1,dp(40)));
        heading=text("E-MAIL",13,PocketArt.CYAN);top.addView(heading,new LayoutParams(0,-1,1));
        guide=button("GUIDE");player=button("PLAYER");Button compose=button("COMPOSE ↗");
        top.addView(guide,new LayoutParams(dp(74),-1));top.addView(player,new LayoutParams(dp(74),-1));top.addView(compose,new LayoutParams(dp(92),-1));
        guide.setOnClickListener(v->send("mailMode","guide"));player.setOnClickListener(v->send("mailMode","player"));compose.setOnClickListener(v->send("mailCompose",""));
        LinearLayout body=new LinearLayout(c);addView(body,new LayoutParams(-1,0,1));
        LinearLayout left=column();PanelSkin.cell(left,false);body.addView(left,new LayoutParams(0,-1,40));
        listScroll=new ScrollView(c);left.addView(listScroll,new LayoutParams(-1,0,1));list=column();listScroll.addView(list,new ScrollView.LayoutParams(-1,-2));
        LinearLayout pages=new LinearLayout(c);left.addView(pages,new LayoutParams(-1,dp(36)));
        previous=button("‹");next=button("›");page=text("1",10,Color.WHITE);page.setGravity(Gravity.CENTER);
        pages.addView(previous,new LayoutParams(0,-1,1));pages.addView(page,new LayoutParams(0,-1,1));pages.addView(next,new LayoutParams(0,-1,1));
        previous.setOnClickListener(v->send("mailPage","previous"));next.setOnClickListener(v->send("mailPage","next"));
        LinearLayout right=column();PanelSkin.cell(right,false);LayoutParams rp=new LayoutParams(0,-1,60);rp.setMargins(dp(4),0,0,0);body.addView(right,rp);
        detailScroll=new ScrollView(c);right.addView(detailScroll,new LayoutParams(-1,0,1));details=column();detailScroll.addView(details,new ScrollView.LayoutParams(-1,-2));
        LinearLayout claim=new LinearLayout(c);right.addView(claim,new LayoutParams(-1,dp(36)));claimItems=button("CLAIM ITEMS");claimTaros=button("CLAIM TAROS");claim.addView(claimItems,new LayoutParams(0,-1,1));claim.addView(claimTaros,new LayoutParams(0,-1,1));
        claimItems.setOnClickListener(v->selectedAction("mailClaimItems"));claimTaros.setOnClickListener(v->selectedAction("mailClaimTaros"));
        LinearLayout actions=new LinearLayout(c);right.addView(actions,new LayoutParams(-1,dp(36)));reply=button("REPLY ↗");delete=button("DELETE");actions.addView(reply,new LayoutParams(0,-1,1));actions.addView(delete,new LayoutParams(0,-1,1));
        reply.setOnClickListener(v->selectedAction("mailReply"));delete.setOnClickListener(v->confirmDelete());
        LinearLayout notice=new LinearLayout(c);addView(notice,new LayoutParams(-1,-2));status=text("",10,PocketArt.CYAN);notice.addView(status,new LayoutParams(0,-2,1));dismiss=button("OK");notice.addView(dismiss,new LayoutParams(dp(50),dp(30)));dismiss.setOnClickListener(v->send("mailDismiss",""));dismiss.setVisibility(GONE);
        setContentDescription("E-Mail. Guide and player mail, unread messages and received attachments.");
    }
    private int dp(int n){return Math.round(n*density);}
    private LinearLayout column(){LinearLayout v=new LinearLayout(getContext());v.setOrientation(VERTICAL);return v;}
    private TextView text(String value,int size,int color){TextView t=new TextView(getContext());t.setText(value);t.setTextSize(size);t.setTextColor(color);t.setPadding(dp(5),dp(4),dp(5),dp(4));return t;}
    private Button button(String label){Button b=new Button(getContext());b.setText(label);b.setTextSize(9);b.setMinWidth(0);b.setMinimumWidth(0);b.setPadding(dp(2),0,dp(2),0);PanelSkin.button(b);return b;}
    private void send(String action,String key){if(!released)PocketDualScreen.send(action,key,0,0);}
    private void selectedAction(String action){if(selected!=null)send(action,selected.optString("key"));}
    void update(JSONObject state,boolean online){
        if(state==null||released)return;selected=state.optJSONObject("detail");boolean isGuide=state.optBoolean("guide"),enabled=online&&!state.optBoolean("busy");
        if(selected!=null&&selected.optString("key").isEmpty())selected=null;
        ready=enabled;
        heading.setText("E-MAIL"+(state.optInt("unread")>0?"  ● "+state.optInt("unread"):""));guide.setSelected(isGuide);player.setSelected(!isGuide);guide.setEnabled(enabled);player.setEnabled(enabled);
        previous.setEnabled(enabled&&state.optBoolean("previous"));next.setEnabled(enabled&&state.optBoolean("next"));page.setText(Integer.toString(state.optInt("page",1)));
        String message=state.optString("message");status.setText(message);dismiss.setVisibility(message.isEmpty()||message.equals("Loading mail…")?GONE:VISIBLE);
        JSONArray entries=state.optJSONArray("entries");String lk=isGuide+":"+state.optInt("page")+":"+String.valueOf(entries);
        if(!lk.equals(listKey)){int y=listScroll.getScrollY();listKey=lk;list.removeAllViews();
            if(entries==null||entries.length()==0)list.addView(text("No messages in this folder.",12,Color.WHITE));
            if(entries!=null)for(int i=0;i<entries.length();i++){JSONObject e=entries.optJSONObject(i);if(e==null)continue;final String key=e.optString("key");boolean chosen=e.optBoolean("selected");
                LinearLayout row=new LinearLayout(getContext());row.setGravity(Gravity.CENTER_VERTICAL);PanelSkin.cell(row,chosen);LayoutParams lp=new LayoutParams(-1,-2);lp.setMargins(dp(2),dp(3),dp(2),0);list.addView(row,lp);
                row.addView(new Icon(e.optString("image")),new LayoutParams(dp(44),dp(48)));LinearLayout words=column();row.addView(words,new LayoutParams(0,-2,1));int ink=chosen?PocketArt.INK:Color.WHITE;
                TextView from=text((e.optBoolean("unread")?"● ":"")+e.optString("from"),11,ink);from.setTypeface(Typeface.DEFAULT_BOLD);words.addView(from);words.addView(text(e.optString("subject"),11,chosen?PocketArt.INK:PocketArt.CYAN));words.addView(text(e.optString("date")+(e.optBoolean("attachments")?" • attachment":""),9,ink));
                row.setOnClickListener(v->{if(ready)send("mailSelect",key);});row.setContentDescription((e.optBoolean("unread")?"Unread. ":"")+e.optString("from")+". "+e.optString("subject"));
            }listScroll.post(()->listScroll.scrollTo(0,y));
        }
        String key=selected==null?"none":selected.optString("key"),dk=String.valueOf(selected);
        if(!dk.equals(detailKey)){int y=key.equals(identity)?detailScroll.getScrollY():0;identity=key;detailKey=dk;details.removeAllViews();
            if(selected==null)details.addView(text("Select a message to read it.",13,Color.WHITE));
            else{LinearLayout head=new LinearLayout(getContext());details.addView(head,new LayoutParams(-1,-2));head.addView(new Icon(selected.optString("image")),new LayoutParams(dp(55),dp(55)));LinearLayout words=column();head.addView(words,new LayoutParams(0,-2,1));words.addView(text(selected.optString("from"),13,Color.WHITE));words.addView(text(selected.optString("subject"),13,PocketArt.CYAN));words.addView(text(selected.optString("date"),9,Color.WHITE));
                details.addView(text(selected.optString("body"),12,Color.WHITE));JSONArray items=selected.optJSONArray("items");
                if(items!=null)for(int i=0;i<items.length();i++){JSONObject item=items.optJSONObject(i);if(item==null)continue;LinearLayout attachment=new LinearLayout(getContext());PanelSkin.cell(attachment,false);details.addView(attachment,new LayoutParams(-1,dp(48)));attachment.addView(new Icon(item.optString("image")),new LayoutParams(dp(44),-1));attachment.addView(text("Attachment ×"+item.optInt("count",1),11,PocketArt.CYAN));}
                if(selected.optInt("taros")>0)details.addView(text(selected.optInt("taros")+" Taros",12,PocketArt.LIME));if(!selected.optString("note").isEmpty())details.addView(text(selected.optString("note"),10,PocketArt.CYAN));
            }detailScroll.post(()->detailScroll.scrollTo(0,y));
        }
        claimItems.setVisibility(!isGuide&&selected!=null&&selected.optJSONArray("items")!=null&&selected.optJSONArray("items").length()>0?VISIBLE:GONE);claimTaros.setVisibility(!isGuide&&selected!=null&&selected.optInt("taros")>0?VISIBLE:GONE);
        claimItems.setEnabled(enabled&&selected!=null&&selected.optBoolean("canClaimItems"));claimTaros.setEnabled(enabled&&selected!=null&&selected.optBoolean("canClaimTaros"));reply.setVisibility(isGuide?GONE:VISIBLE);delete.setVisibility(isGuide?GONE:VISIBLE);reply.setEnabled(enabled&&selected!=null&&selected.optBoolean("canReply"));delete.setEnabled(enabled&&selected!=null&&selected.optBoolean("canDelete"));
        ((View)reply.getParent()).setVisibility(isGuide?GONE:VISIBLE);((View)claimItems.getParent()).setVisibility(claimItems.getVisibility()==VISIBLE||claimTaros.getVisibility()==VISIBLE?VISIBLE:GONE);
    }
    private void confirmDelete(){if(selected==null||confirmation!=null)return;final String key=selected.optString("key");confirmation=new AlertDialog.Builder(getContext()).setTitle("Delete message?").setMessage("Unclaimed attachments may be lost.\n\n"+selected.optString("subject")).setNegativeButton("Cancel",(d,w)->{}).setPositiveButton("Delete",(d,w)->send("mailDelete",key)).create();confirmation.setOnDismissListener(d->confirmation=null);confirmation.show();}
    void invalidateImages(){invalidateTree(list);invalidateTree(details);}
    private void invalidateTree(ViewGroup parent){for(int i=0;i<parent.getChildCount();i++){View v=parent.getChildAt(i);v.invalidate();if(v instanceof ViewGroup)invalidateTree((ViewGroup)v);}}
    void release(){released=true;if(confirmation!=null)confirmation.dismiss();}
    protected void onDetachedFromWindow(){if(confirmation!=null)confirmation.dismiss();super.onDetachedFromWindow();}
    private final class Icon extends View {
        private final String file;private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
        Icon(String path){super(EmailPane.this.getContext());file=path;}
        protected void onDraw(Canvas c){Bitmap b=images.get(file);if(b==null||b.isRecycled())return;float s=Math.min((float)getWidth()/b.getWidth(),(float)getHeight()/b.getHeight()),w=b.getWidth()*s,h=b.getHeight()*s;c.drawBitmap(b,null,new RectF((getWidth()-w)/2,(getHeight()-h)/2,(getWidth()+w)/2,(getHeight()+h)/2),paint);}
    }
}
