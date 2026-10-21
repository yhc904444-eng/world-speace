package eng.yhc904444.worldspeace;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.Voice;
import android.util.AtomicFile;
import android.view.View;
import android.view.WindowInsets;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.Toast;
import java.io.*;
import java.nio.ByteBuffer;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.util.*;
import org.json.*;

public final class MainActivity extends Activity {
    private static final String HOST="appassets.androidplatform.net";
    private static final int OPEN=41,CREATE=42,MAX_BYTES=20*1024*1024;
    private WebView web;
    private TextToSpeech tts;
    private volatile boolean speechReady;
    private final Object stateLock=new Object();
    private String pendingKind,pendingExport;
    private volatile byte[] picked;
    private AtomicFile stateFile(){return new AtomicFile(new File(getFilesDir(),"progress.json"));}
    private AtomicFile previousFile(){return new AtomicFile(new File(getFilesDir(),"before-restore.json"));}

    @Override public void onCreate(Bundle saved){
        super.onCreate(saved);
        FrameLayout root=new FrameLayout(this);root.setBackgroundColor(Color.rgb(241,246,242));
        web=new WebView(this);root.addView(web,new FrameLayout.LayoutParams(-1,-1));setContentView(root);
        if(Build.VERSION.SDK_INT>=30){
            getWindow().setDecorFitsSystemWindows(false);
            root.setOnApplyWindowInsetsListener((view,insets)->{android.graphics.Insets bars=insets.getInsets(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());android.graphics.Insets ime=insets.getInsets(WindowInsets.Type.ime());view.setPadding(bars.left,bars.top,bars.right,Math.max(bars.bottom,ime.bottom));return insets;});
        }else root.setFitsSystemWindows(true);
        WebSettings s=web.getSettings();s.setJavaScriptEnabled(true);s.setDomStorageEnabled(true);
        s.setAllowFileAccess(false);s.setAllowContentAccess(false);s.setAllowFileAccessFromFileURLs(false);s.setAllowUniversalAccessFromFileURLs(false);
        s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);s.setJavaScriptCanOpenWindowsAutomatically(false);s.setSupportMultipleWindows(false);
        s.setMediaPlaybackRequiresUserGesture(true);s.setDefaultTextEncodingName("UTF-8");s.setTextZoom(100);
        WebView.setWebContentsDebuggingEnabled(false);
        web.addJavascriptInterface(new Bridge(),"Android");web.setWebChromeClient(new WebChromeClient());
        web.setWebViewClient(new WebViewClient(){
            @Override public boolean shouldOverrideUrlLoading(WebView v,WebResourceRequest request){return !isMainPage(request.getUrl());}
            @Override public boolean shouldOverrideUrlLoading(WebView v,String url){return !isMainPage(Uri.parse(url));}
            @Override public WebResourceResponse shouldInterceptRequest(WebView v,WebResourceRequest request){
                Uri uri=request.getUrl();if(!"GET".equals(request.getMethod())||!"https".equals(uri.getScheme())||!HOST.equals(uri.getHost()))return response(403,"Forbidden",new byte[0],"text/plain");
                String path=uri.getPath();
                if("/import.txt".equals(path)){byte[] data=picked;return data==null?response(404,"Not Found",new byte[0],"text/plain"):response(200,"OK",data,"text/plain");}
                if(path==null||!path.startsWith("/assets/")||path.contains("..")||path.indexOf('\\')>=0)return response(403,"Forbidden",new byte[0],"text/plain");
                String asset=path.substring(8);String mime=asset.endsWith(".html")?"text/html":asset.endsWith(".mjs")?"text/javascript":asset.endsWith(".css")?"text/css":asset.endsWith(".png")?"image/png":asset.endsWith(".json")?"application/json":"text/plain";
                try{Map<String,String> headers=headers();return new WebResourceResponse(mime,"UTF-8",200,"OK",headers,getAssets().open(asset));}catch(IOException error){return response(404,"Not Found",new byte[0],"text/plain");}
            }
        });
        tts=new TextToSpeech(this,status->web.post(()->{speechReady=status==TextToSpeech.SUCCESS;if(speechReady&&tts!=null){tts.setSpeechRate(.85f);tts.setLanguage(Locale.US);}}));
        web.loadUrl("https://"+HOST+"/assets/index.html");
        if(Build.VERSION.SDK_INT>=33)getOnBackInvokedDispatcher().registerOnBackInvokedCallback(0,()->handleBack());
    }
    private boolean isMainPage(Uri uri){return "https".equals(uri.getScheme())&&HOST.equals(uri.getHost())&&"/assets/index.html".equals(uri.getPath());}
    private Map<String,String> headers(){Map<String,String> h=new HashMap<>();h.put("Content-Security-Policy","default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'");h.put("X-Content-Type-Options","nosniff");h.put("Cache-Control","no-store");return h;}
    private WebResourceResponse response(int code,String reason,byte[] bytes,String mime){return new WebResourceResponse(mime,"UTF-8",code,reason,headers(),new ByteArrayInputStream(bytes));}
    private byte[] limited(InputStream stream) throws IOException {
        try(InputStream in=stream;ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buffer=new byte[16384];int n;while((n=in.read(buffer))!=-1){if(out.size()+n>MAX_BYTES)throw new IOException("文件不能超过 20 MB。");out.write(buffer,0,n);}return out.toByteArray();}
    }
    private String read(AtomicFile file) throws IOException {return new String(limited(file.openRead()),StandardCharsets.UTF_8);}
    private void write(AtomicFile file,String text) throws IOException {
        byte[] bytes=text.getBytes(StandardCharsets.UTF_8);if(bytes.length>MAX_BYTES)throw new IOException("进度文件超过 20 MB，请先导出备份。");FileOutputStream out=null;
        try{out=file.startWrite();out.write(bytes);file.finishWrite(out);}catch(IOException error){if(out!=null)file.failWrite(out);throw error;}
    }
    private JSONObject envelope(boolean ok,String field,String value){JSONObject result=new JSONObject();try{result.put("ok",ok);result.put(field,value);}catch(JSONException ignored){}return result;}
    private void event(String kind,JSONObject payload){runOnUiThread(()->{try{payload.put("kind",kind);}catch(JSONException ignored){}web.evaluateJavascript("window.dispatchEvent(new CustomEvent('native-result',{detail:"+payload.toString()+"}))",null);});}
    private void notifyUser(String text){runOnUiThread(()->Toast.makeText(this,text,Toast.LENGTH_LONG).show());}
    public final class Bridge {
        @JavascriptInterface public String readState(){synchronized(stateLock){try{if(!stateFile().getBaseFile().exists()&&!new File(getFilesDir(),"progress.json.bak").exists())return envelope(true,"state","").toString();String value=read(stateFile());if(new JSONObject(value).optInt("Version")!=1)throw new IOException("进度版本不受支持。");return envelope(true,"state",value).toString();}catch(Exception error){return envelope(false,"error","学习记录无法读取，原文件已保留。请先导出原始进度再处理。").toString();}}}
        @JavascriptInterface public String saveState(String value){synchronized(stateLock){try{JSONObject parsed=new JSONObject(value);if(parsed.optInt("Version")!=1||!parsed.has("Cards"))return "进度格式无效。";write(stateFile(),value);return "";}catch(Exception error){return "进度未保存："+error.getMessage();}}}
        @JavascriptInterface public String preserveBeforeRestore(){synchronized(stateLock){try{if(stateFile().getBaseFile().exists())write(previousFile(),read(stateFile()));return "";}catch(Exception error){return "恢复前备份失败，未替换进度。";}}}
        @JavascriptInterface public String previousBackup(){synchronized(stateLock){try{return read(previousFile());}catch(Exception error){return "";}}}
        @JavascriptInterface public String rawState(){synchronized(stateLock){try{return read(stateFile());}catch(Exception error){return "";}}}
        @JavascriptInterface public String voices(){JSONArray values=new JSONArray();if(speechReady&&tts.getVoices()!=null)for(Voice v:tts.getVoices())if("en".equals(v.getLocale().getLanguage())&&!v.isNetworkConnectionRequired())values.put(v.getName());return values.toString();}
        @JavascriptInterface public String speak(String text,String requested){
            if(!speechReady)return "英语语音尚未就绪，请在系统语音设置中安装离线英语语音包。";
            Voice selected=null;if(tts.getVoices()!=null)for(Voice v:tts.getVoices())if("en".equals(v.getLocale().getLanguage())&&!v.isNetworkConnectionRequired()){if(selected==null)selected=v;if(v.getName().equals(requested)){selected=v;break;}}
            if(selected==null)return "未找到离线英语语音，请先在系统语音设置中下载英语语音包。";
            final Voice voice=selected;final String phrase=text.length()>500?text.substring(0,500):text;runOnUiThread(()->{tts.setVoice(voice);if(tts.speak(phrase,TextToSpeech.QUEUE_FLUSH,null,"word")<0)notifyUser("朗读失败，请检查系统语音引擎。");});return "";
        }
        @JavascriptInterface public void stopSpeech(){runOnUiThread(()->{if(tts!=null)tts.stop();});}
        @JavascriptInterface public void pickDocument(String kind){if(!"backup".equals(kind)&&!"book".equals(kind))return;runOnUiThread(()->{if(pendingKind!=null){notifyUser("请先完成当前文件操作。");return;}pendingKind=kind;Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("*/*");try{startActivityForResult(intent,OPEN);}catch(Exception error){pendingKind=null;notifyUser("无法打开系统文件选择器。");}});}
        @JavascriptInterface public void exportDocument(String name,String mime,String text){if(text.getBytes(StandardCharsets.UTF_8).length>MAX_BYTES){notifyUser("导出文件超过 20 MB。");return;}runOnUiThread(()->{if(pendingExport!=null){notifyUser("请先完成当前导出。");return;}pendingExport=text;Intent intent=new Intent(Intent.ACTION_CREATE_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("application/json".equals(mime)?mime:"text/csv");intent.putExtra(Intent.EXTRA_TITLE,name.replace('/','_').replace('\\','_'));try{startActivityForResult(intent,CREATE);}catch(Exception error){pendingExport=null;notifyUser("无法打开系统保存位置选择器。");}});}
        @JavascriptInterface public void releasePicked(){picked=null;}
        @JavascriptInterface public void closeApp(){runOnUiThread(()->finish());}
    }
    @Override protected void onActivityResult(int request,int result,Intent data){
        super.onActivityResult(request,result,data);
        if(request==OPEN){final String kind=pendingKind;pendingKind=null;if(result!=RESULT_OK||data==null||data.getData()==null)return;final Uri uri=data.getData();new Thread(()->{try{byte[] bytes=limited(getContentResolver().openInputStream(uri));StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes));picked=bytes;JSONObject payload=new JSONObject();payload.put("operation",kind);payload.put("length",bytes.length);payload.put("name",displayName(uri));event("picked",payload);}catch(Exception error){notifyUser("导入失败：请使用不超过 20 MB 的 UTF-8 文件。");}},"document-import").start();}
        if(request==CREATE){final String text=pendingExport;pendingExport=null;if(result!=RESULT_OK||data==null||data.getData()==null||text==null)return;final Uri uri=data.getData();new Thread(()->{try(OutputStream out=getContentResolver().openOutputStream(uri,"wt")){if(out==null)throw new IOException("无法写入文件");out.write(text.getBytes(StandardCharsets.UTF_8));event("exported",new JSONObject());}catch(Exception error){notifyUser("文件未导出："+error.getMessage());}},"document-export").start();}
    }
    private String displayName(Uri uri){try(android.database.Cursor cursor=getContentResolver().query(uri,new String[]{android.provider.OpenableColumns.DISPLAY_NAME},null,null,null)){if(cursor!=null&&cursor.moveToFirst())return cursor.getString(0);}catch(Exception ignored){}return "导入的词书";}
    private void handleBack(){web.evaluateJavascript("window.WorldSpeace ? window.WorldSpeace.back() : false",value->{if(!"true".equals(value))finish();});}
    @Override public void onBackPressed(){handleBack();}
    @Override protected void onPause(){super.onPause();if(tts!=null)tts.stop();if(web!=null)web.onPause();}
    @Override protected void onResume(){super.onResume();if(web!=null)web.onResume();}
    @Override protected void onDestroy(){speechReady=false;if(tts!=null){tts.stop();tts.shutdown();}if(web!=null){web.removeJavascriptInterface("Android");web.destroy();}super.onDestroy();}
}
