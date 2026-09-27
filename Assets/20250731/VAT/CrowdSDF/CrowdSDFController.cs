using UnityEngine;
using UnityEngine.VFX;

namespace VATCrowd
{
    [ExecuteAlways]
    public sealed class CrowdSDFController : MonoBehaviour
    {
        [Header("Comparison")]
        public VisualEffect effect;
        public VisualEffect originalEffect;
        public bool showOriginal;
        public bool showControls = true;
        [Header("Crowd (GPU, no inter-person collisions)")]
        [Range(1,1024)] public int count = 50;
        [Range(0,6)] public float walkSpeed = 2;
        [Range(.1f,1)] public float radius = .4f;
        [Range(.5f,5)] public float lookAhead = 2;
        [Range(.1f,3)] public float avoidStrength = 1.3f;
        [Range(.5f,15)] public float turnRate = 6;
        [Tooltip("World-space distance from the VAT pivot at the feet to the collision sample.")]
        public float bodyHeight = .9f;
        public float groundY;
        [Tooltip("World speed matching the original VAT clip's 0.3 playback multiplier.")]
        public float referenceSpeed = 2;
        [Header("Static SDF: world-space, axis-aligned bake volume")]
        public Texture3D distanceField;
        public Vector3 fieldCenter = new Vector3(1,2,19);
        public Vector3 fieldSize = new Vector3(32,8,64);
        public Transform collisionProxies;
        [Header("Bezier route (world coordinates)")]
        public Vector3 p0, p1, p2, p3;
        public bool showFieldBounds = true;
        public bool showProxyWireframes;
        int appliedCount = -1;
        bool appliedOriginal;
        float smoothedFrame;
        bool pending;
        void OnEnable() { pending=true; }
        void OnValidate()
        {
            count=Mathf.Clamp(count,1,1024);radius=Mathf.Clamp(radius,.1f,1);
            fieldSize=new Vector3(Mathf.Max(.01f,fieldSize.x),Mathf.Max(.01f,fieldSize.y),Mathf.Max(.01f,fieldSize.z));
            pending=true;
        }
        void Update()
        {
            if(pending || appliedCount!=count || appliedOriginal!=showOriginal) {Apply();pending=false;}
            if(Application.isPlaying) smoothedFrame=Mathf.Lerp(smoothedFrame,Time.unscaledDeltaTime,.04f);
        }
        [ContextMenu("Apply settings / Restart crowd")]
        public void Restart() {Apply();if(effect!=null)effect.Reinit();}
        public void Apply()
        {
            if(effect==null || distanceField==null)return;
            effect.gameObject.SetActive(!showOriginal);
            if(originalEffect!=null)originalEffect.gameObject.SetActive(showOriginal);
            Set("WalkSpeed",walkSpeed);Set("Radius",radius);Set("LookAhead",lookAhead);
            Set("AvoidStrength",avoidStrength);Set("TurnRate",turnRate);
            Set("BodyHeight",bodyHeight);Set("GroundY",groundY);Set("ReferenceSpeed",referenceSpeed);
            Set("CrowdCount",count);
            if(effect.HasTexture("DistanceField"))effect.SetTexture("DistanceField",distanceField);
            Vector("FieldCenter",fieldCenter);Vector("FieldSize",fieldSize);
            Vector("P0",p0);Vector("P1",p1);Vector("P2",p2);Vector("P3",p3);
            if(appliedCount!=count || appliedOriginal!=showOriginal)effect.Reinit();
            appliedCount=count;appliedOriginal=showOriginal;
        }
        void Set(string n,float v){if(effect.HasFloat(n))effect.SetFloat(n,v);}
        void Vector(string n,Vector3 v){if(effect.HasVector3(n))effect.SetVector3(n,v);}
        void OnGUI()
        {
            if(!Application.isPlaying || !showControls)return;
            GUILayout.BeginArea(new Rect(16,16,285,170),GUI.skin.box);
            GUILayout.Label("VAT Crowd | SDF environment avoidance");
            bool original=GUILayout.Toggle(showOriginal,"Show original effect");
            if(original!=showOriginal){showOriginal=original;pending=true;}
            GUILayout.Label(count+" people   |   "+(smoothedFrame*1000).ToString("F1")+" ms/frame");
            GUILayout.Label("Walk speed: "+walkSpeed.ToString("F1")+" m/s");
            float speed=GUILayout.HorizontalSlider(walkSpeed,0,6);
            if(!Mathf.Approximately(speed,walkSpeed)){walkSpeed=speed;pending=true;}
            if(GUILayout.Button("Restart crowd"))Restart();
            GUILayout.EndArea();
        }
        void OnDrawGizmosSelected()
        {
            if(showFieldBounds){Gizmos.color=Color.cyan;Gizmos.DrawWireCube(fieldCenter,fieldSize);}
            Gizmos.color=Color.yellow;Vector3 last=p0;
            for(int i=1;i<=64;i++) {float t=i/64f,u=1-t;var p=u*u*u*p0+3*u*u*t*p1+3*u*t*t*p2+t*t*t*p3;Gizmos.DrawLine(last,p);last=p;}
            Gizmos.DrawWireSphere(p0,.5f);Gizmos.DrawWireSphere(p3,.5f);
            if(showProxyWireframes && collisionProxies!=null)
            {
                Gizmos.color=new Color(1,.45f,.1f,.8f);
                foreach(var mesh in collisionProxies.GetComponentsInChildren<MeshFilter>())
                    Gizmos.DrawWireMesh(mesh.sharedMesh,mesh.transform.position,mesh.transform.rotation,mesh.transform.lossyScale);
            }
        }
    }
}
