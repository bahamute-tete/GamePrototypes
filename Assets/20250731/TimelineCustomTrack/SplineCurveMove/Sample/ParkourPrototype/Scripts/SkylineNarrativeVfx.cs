using UnityEngine;
using UnityEngine.Playables;

/// <summary>Time-addressable effects. Drone travel stays on the user's SplineCurveMove track.</summary>
[ExecuteAlways, DefaultExecutionOrder(-100)]
public class SkylineNarrativeVfx : MonoBehaviour
{
    public PlayableDirector director;
    public Transform actor, drone, leftHand, rightHand, duoFocus, shotFocus;
    public Transform target, bolt, flash, charge, shockwave, bow;
    public LineRenderer boltTrail, scanner, bowString;
    public LineRenderer[] sparks;
    public Transform[] targetRings;
    public ParticleSystem impactParticles;
    public ParticleSystem[] ambientParticles;
    public Vector3 launchPoint, impactPoint;
    public float chargeStart=22.1f, releaseTime=23.8f, flightDuration=.42f;
    float previousTime=-100;
    void LateUpdate() { if(director) Evaluate((float)director.time); }
    public void Evaluate(float t)
    {
        if(!actor || !drone)return;
        if(duoFocus)duoFocus.position=Vector3.Lerp(actor.position+Vector3.up*1.2f,drone.position,.36f);
        if(shotFocus)shotFocus.position=Vector3.Lerp(launchPoint,impactPoint,.55f);
        float impact=releaseTime+flightDuration;
        bool charging=t>=chargeStart && t<releaseTime;
        float chargeProgress=Mathf.Clamp01((t-chargeStart)/(releaseTime-chargeStart));
        if(charge){charge.gameObject.SetActive(charging);charge.position=leftHand.position;charge.rotation=Quaternion.LookRotation(impactPoint-leftHand.position);charge.localScale=Vector3.one*(.035f+.16f*chargeProgress);}
        if(bow)
        {
            bow.gameObject.SetActive(t>=21.25f && t<24.55f);bow.position=leftHand.position;
            bow.rotation=Quaternion.LookRotation((impactPoint-leftHand.position).normalized,Vector3.up);
        }
        if(bowString && bow)
        {
            bowString.SetPosition(0,bow.TransformPoint(new Vector3(0,-.55f,.10f)));
            bowString.SetPosition(1,t>=22.3f&&t<releaseTime?rightHand.position:bow.position);
            bowString.SetPosition(2,bow.TransformPoint(new Vector3(0,.55f,.10f)));
        }
        float travel=Mathf.Clamp01((t-releaseTime)/flightDuration);
        if(bolt){bolt.gameObject.SetActive(t>=releaseTime && t<impact);bolt.position=Vector3.Lerp(launchPoint,impactPoint,travel);bolt.rotation=Quaternion.LookRotation(impactPoint-launchPoint);}
        if(boltTrail)
        {
            bool active=t>=releaseTime && t<impact+.1f;boltTrail.enabled=active;
            Vector3 head=Vector3.Lerp(launchPoint,impactPoint,travel),tail=Vector3.Lerp(launchPoint,impactPoint,Mathf.Clamp01((t-releaseTime-.055f)/flightDuration));
            boltTrail.SetPosition(0,tail);boltTrail.SetPosition(1,head);
            boltTrail.widthMultiplier=Mathf.Max(0,.06f*(1-Mathf.Clamp01((t-impact)/.1f)));
        }
        if(flash){float age=t-releaseTime;flash.gameObject.SetActive(age>=0&&age<.13f);flash.position=launchPoint;flash.localScale=Vector3.one*Mathf.Lerp(.3f,.015f,Mathf.Clamp01(age/.13f));}
        float hitAge=t-impact;
        if(target)target.gameObject.SetActive(t<impact+.08f);
        if(targetRings!=null)for(int i=0;i<targetRings.Length;i++)if(targetRings[i])targetRings[i].localRotation=Quaternion.Euler(0,0,(i%2==0?1:-1)*(t*35+i*25));
        if(shockwave){shockwave.gameObject.SetActive(hitAge>=0&&hitAge<.7f);shockwave.position=impactPoint;shockwave.localScale=Vector3.one*(.15f+Mathf.Clamp01(hitAge/.7f)*2.4f);var lr=shockwave.GetComponent<LineRenderer>();if(lr)lr.widthMultiplier=.065f*(1-Mathf.Clamp01(hitAge/.7f));}
        if(sparks!=null)for(int i=0;i<sparks.Length;i++)
        {
            var l=sparks[i];l.enabled=hitAge>=0&&hitAge<.8f;
            float angle=i*2.399963f;Vector3 dir=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),-.3f+(i%3)*.13f).normalized;
            float age=Mathf.Clamp(hitAge,0,.8f);Vector3 h=impactPoint+dir*age*(2.5f+(i%4)) +Vector3.down*age*age*2;
            l.SetPosition(0,h-dir*.18f*(1-age));l.SetPosition(1,h);l.widthMultiplier=.027f*(1-age/.8f);
        }
        if(scanner)
        {
            bool on=t>=14.2f && t<23.4f;scanner.enabled=on;
            scanner.SetPosition(0,drone.position+Vector3.down*.25f);
            scanner.SetPosition(1,impactPoint+new Vector3(Mathf.Sin(t*2.2f)*.3f,Mathf.Cos(t*2.2f)*.3f,0));
        }
        if(impactParticles)
        {
            impactParticles.gameObject.SetActive(hitAge>=0&&hitAge<1.4f);
            if(hitAge>=0&&hitAge<1.4f)impactParticles.Simulate(hitAge,true,true,true);
        }
        if(ambientParticles!=null && Mathf.Abs(t-previousTime)>.0001f)
        {
            float delta=t-previousTime;bool restart=delta<0 || delta>.2f;
            foreach(var ps in ambientParticles)if(ps)ps.Simulate(restart?t:delta,true,restart,true);
        }
        previousTime=t;
    }
}
