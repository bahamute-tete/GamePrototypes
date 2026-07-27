
float4 aabbTriangle( in float2 p0, in float2 p1, in float2 p2)
{
    return float4( min(p0,min(p1,p2)),
                 max(p0,max(p1,p2)) );
}

float4 aabbOrientedBox( in float2 a, in float2 b, in float r )
{
    float2 v = r*abs(normalize(float2(b.y-a.y,b.x-a.x)));
    return float4(min(a,b)-v,max(a,b)+v);
}

float4 aabbSegment( in float2 a, in float2 b, in float r )
{
    return float4(min(a,b)-r,max(a,b)+r);
}

float4 boxPie( in float2 c, in float2 d, in float a, in float r )
{
    float si = sin(a);
    float co = cos(a);
    float2 m =    (d.xy)*co;
    float2 n = abs(d.yx)*si;
    return c.xyxy + r*float4(
       (d.x>-co) ? min(m.x-n.x,0.0) : -1.0,
       (d.y>-co) ? min(m.y-n.y,0.0) : -1.0,
       (d.x< co) ? max(m.x+n.x,0.0) :  1.0,
       (d.y< co) ? max(m.y+n.y,0.0) :  1.0 );
}

float4 aabbBezier( in float2 p0, in float2 p1, in float2 p2 )
{
    float2 a = p0-2.0*p1+p2;
    float2 b = p1-p0;
    float2 t = clamp(-b/a,0.0,1.0);
    float2 q = p0+t*(2.0*b+t*a);
    return float4(min(min(p0,p2),q),
                max(max(p0,p2),q));
}

float4 aabbBezier( in float2 p0, in float2 p1, in float2 p2, in float2 p3 )
{
    float2 c  = -p0+    p1;
    float2 b  =  p0-2.0*p1+    p2;
    float2 a  = -p0+3.0*p1-3.0*p2+p3;
    float2 g  = sqrt(max(b*b-a*c,0.0));
    float2 t1 = clamp((-b-g)/a,0.0,1.0);
    float2 t2 = clamp((-b+g)/a,0.0,1.0);
    float2 q1 = p0+t1*(3.0*c+t1*(3.0*b+t1*a));
    float2 q2 = p0+t2*(3.0*c+t2*(3.0*b+t2*a));
    return float4(min(min(p0,p3),min(q1,q2)),
                max(max(p0,p3),max(q1,q2)));
}


float4 aabbParabola( in float w, in float h, in float r )
{
    return float4(-w-r,min(h,0.0)-r,
                 w+r,max(h,0.0)+r);
}

float4 aabbCutDisk( in float r, in float h )
{
    float m = h>0.0 ? sqrt(r*r-h*h) : r;
    return float4(-m,h,m,r);
}

float4 aabbEgg( in float he, in float ra, in float rb, in float bu )
{
    float wi = max(ra, rb);
    float r = 0.5*(he+ra+rb)/bu;
    float da = r - ra;
    float db = r - rb;
    float h = db*db - da*da;
    if( abs(h)<he*he )
    {
        float y = (h-he*he)/(2.0*he);
        wi = max(wi, r - sqrt(da*da-y*y));
    }
    return float4(-wi, -ra, wi, he + rb);
}

float4 boxStar( in float r, in int n, in float w)
{
    float an = 6.283185/float(n);
    float2 kk = float2( cos( round(float(n)/2.0)*an ),
                    sin( round(float(n)/4.0)*an ) );
    return r*float4(-kk.y,kk.x,kk.y,1.0);
}

float4 boxVesicaSegment( in float2 a, in float2 b, in float w )
{
    float2  c  = (b+a)*0.5;
    float2  v  = (b-a)*0.5;
    float v2 = dot(v,v);
    float d  = 0.5*(v2-w*w)/w;
    float h  = -v2/(d+w);
    float2  p  = abs(v.yx)*d/sqrt(v2);
    float2  q  = max(p-d-w,h);
    return float4( min(min(a,b),c+q), 
                 max(max(a,b),c-q) );
}