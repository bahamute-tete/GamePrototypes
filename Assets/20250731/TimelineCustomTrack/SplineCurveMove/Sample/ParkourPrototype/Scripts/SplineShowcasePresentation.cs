using UnityEngine;
using UnityEngine.Playables;

/// <summary>Presentation only. All demonstrated positions are driven by SplineCurveMove tracks.</summary>
[ExecuteAlways]
public class SplineShowcasePresentation : MonoBehaviour
{
    public PlayableDirector director;
    public Transform actorRoot, animationLayer;
    public Camera showcaseCamera, overviewCamera;
    public Transform cameraAim;
    public GameObject routeVisuals;
    public LineRenderer[] routeLines;
    public Transform[] billboardLabels;
    public SplineEventReceiver[] receivers;
    public bool showRoutes = true;
    string lastEvent = "Waiting for a path event";
    int eventCount;
    public int EventCount => eventCount;
    public string LastEvent => lastEvent;
    GUIStyle title, body, small;
    static readonly float[] starts = {0,6,19,26,32,38,51};
    static readonly string[] chapters = {"01  DEPARTURE", "02  MOVING REFERENCE", "03  PAUSE / ACTION", "04  CONSTANT-SPEED RUN", "05  MOVING OBSTACLE", "06  ASCENT / END MODES", "07  ARRIVAL"};
    static readonly string[] details = {
        "World path  /  Idle > walk  /  Auto-matched boarding transition",
        "Actor walks on a moving deck  /  ReferenceFrame  /  Look offset",
        "Flat displacement keys hold position  /  Separate action animation",
        "Arc-length movement  /  Y-axis rotation lock  /  Animation blend",
        "Obstacle slides on its own spline  /  Reverse progress returns it",
        "Elevating reference frame  /  Cyan follows; amber holds world position",
        "Finish event  /  Camera orbit  /  Replay or scrub any chapter"};

    void OnEnable() { if(receivers!=null) foreach(var r in receivers) if(r) r.OnAnyEvent += Receive; }
    void OnDisable() { if(receivers!=null) foreach(var r in receivers) if(r) r.OnAnyEvent -= Receive; }
    void Receive(SplinePathEvent e) { lastEvent=e.eventName+"  /  "+e.parameter; eventCount++; }
    public void RefreshPresentation()
    {
        // Humanoid action layer cannot contribute world travel on top of the Root spline.
        if(animationLayer) { animationLayer.localPosition=Vector3.zero; animationLayer.localRotation=Quaternion.identity; }
        if(showcaseCamera && cameraAim) showcaseCamera.transform.LookAt(cameraAim.position,Vector3.up);
        if(routeVisuals && routeVisuals.activeSelf != showRoutes) routeVisuals.SetActive(showRoutes);
        if(routeLines!=null) foreach(var line in routeLines) if(line)line.enabled=showRoutes;
        if(billboardLabels!=null) foreach(var label in billboardLabels) if(label && showcaseCamera)label.rotation=showcaseCamera.transform.rotation;
    }
    void LateUpdate() { RefreshPresentation(); }
    void Update()
    {
        if(!Application.isPlaying || !director) return;
        if(Input.GetKeyDown(KeyCode.Space)) TogglePlayback();
        if(Input.GetKeyDown(KeyCode.R)) Seek(0,true);
        if(Input.GetKeyDown(KeyCode.V)) showRoutes=!showRoutes;
        if(Input.GetKeyDown(KeyCode.C)) ToggleCamera();
    }
    void ToggleCamera()
    {
        if(!showcaseCamera || !overviewCamera) return;
        showcaseCamera.enabled=!showcaseCamera.enabled; overviewCamera.enabled=!showcaseCamera.enabled;
    }
    public void Seek(double time,bool play)
    {
        director.time=time; director.Evaluate(); RefreshPresentation();
        if(play) director.Play(); else director.Pause();
        lastEvent="Seek: path events resume on playback"; eventCount=0;
    }
    void TogglePlayback() { if(director.state==PlayState.Playing) director.Pause(); else director.Play(); }
    void OnGUI()
    {
        if(!Application.isPlaying || !director) return;
        if(title==null)
        {
            title=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold}; title.normal.textColor=Color.white;
            body=new GUIStyle(GUI.skin.label){fontSize=15};body.normal.textColor=new Color(.73f,.93f,.98f);
            small=new GUIStyle(GUI.skin.label){fontSize=12};small.normal.textColor=new Color(.7f,.78f,.85f);
        }
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        var old=GUI.matrix; GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
        float w=Screen.width/scale, h=Screen.height/scale;
        int chapter=0;for(int i=0;i<starts.Length;i++)if(director.time>=starts[i])chapter=i;
        GUI.color=new Color(.02f,.04f,.075f,.90f);GUI.DrawTexture(new Rect(24,24,640,120),Texture2D.whiteTexture);
        GUI.color=new Color(.05f,.87f,.82f);GUI.DrawTexture(new Rect(24,24,4,120),Texture2D.whiteTexture);GUI.color=Color.white;
        GUI.Label(new Rect(44,31,590,25),"SPLINE / SKYLINE     •     TRANSFORM WAYPOINT SHOWCASE",small);
        GUI.Label(new Rect(44,57,590,34),chapters[chapter],title);
        GUI.Label(new Rect(44,100,600,32),details[chapter],small);
        GUI.color=new Color(.02f,.04f,.075f,.94f);GUI.DrawTexture(new Rect(24,h-120,w-48,96),Texture2D.whiteTexture);GUI.color=Color.white;
        GUI.Label(new Rect(42,h-114,750,25),$"{director.time:00.0} / 64.0 s    |    EVENTS {eventCount:00}    {lastEvent}",small);
        if(GUI.Button(new Rect(42,h-81,78,26),director.state==PlayState.Playing?"PAUSE":"PLAY"))TogglePlayback();
        if(GUI.Button(new Rect(128,h-81,74,26),"REPLAY"))Seek(0,true);
        float scrub=GUI.HorizontalSlider(new Rect(222,h-72,w-562,20),(float)director.time,0,63.99f);
        if(Mathf.Abs(scrub-(float)director.time)>.12f)Seek(scrub,false);
        if(GUI.Button(new Rect(w-312,h-81,124,26),showRoutes?"PATHS: ON":"PATHS: OFF"))showRoutes=!showRoutes;
        if(GUI.Button(new Rect(w-180,h-81,130,26),"CAMERA / C"))ToggleCamera();
        for(int i=0;i<starts.Length;i++)if(GUI.Button(new Rect(42+i*107,h-47,99,19),$"{i+1:00}  /  {starts[i]:00}s"))Seek(starts[i],true);
        GUI.Label(new Rect(w-390,h-44,350,21),"SPACE pause   R replay   V paths   C overview",small);
        GUI.matrix=old;
    }
}
