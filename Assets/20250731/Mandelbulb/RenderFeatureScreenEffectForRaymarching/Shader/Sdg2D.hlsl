// .x = f(p)
// .y = ∂f(p)/∂x
// .z = ∂f(p)/∂y
// .yz = ∇f(p) with ‖∇f(p)‖ = 1
// sc = sin/cos of aperture

float cro( in float2 a, in float2 b ) { return a.x*b.y - a.y*b.x; }

// float3 sdgRound( in float2 p, in float r )
// {
//     float3 dis_gra = sdgShape(p);
//     return float3( dis_gra.x - r, dis_gra.yz );
// }

// float3 sdgOnion( in float2 p, in float r )
// {
//     float3 dis_gra = sdgShape(p);
//     return float3( abs(dis_gra.x) - r, sign(dis_gra.x)*dis_gra.yz );
// }

float3 sdgCircle(in float2 p, in float r ) 
{
    float d = length(p);
    return float3( d-r, p/d );
}

float3 sdgPie( in float2 p, in float2 sc, in float r )
{
    float s = sign(p.x); p.x = abs(p.x);
    float l = length(p);
    float n = l - r;
    float2  q = p - sc*clamp(dot(p,sc),0.0,r);
    float m = length(q) * sign(sc.y*p.x-sc.x*p.y);
    float3  res = (n>m) ? float3(n,p/l) : float3(m,q/m);
    return float3(res.x,s*res.y,res.z);
}

// axis aligne pie: an=angle, ra=radius
float sdPie( float2 p, float2 ce, float an, float ra )
{
    p -= ce;
    float2 sc = float2(sin(an),cos(an));
    p.x = abs(p.x);
    float l = length(p) - ra;
	float m = length(p - sc*clamp(dot(p,sc),0.0,ra) );
    return max(l,m*sign(sc.y*p.x-sc.x*p.y));
}

// arbitrary pie: ce=center, di=direction, an=angle, ra=radius
float sdPie( float2 p, float2 ce, float2 di, float an, float ra )
{
    p = ce+mul(float2x2(di.y,di.x,-di.x,di.y),(p-ce));
    return sdPie(p,ce,an,ra);
}

float3 sdgArc( in float2 p, in float2 sc, in float ra, in float rb )
{
    float2 q = p;
    float s = sign(p.x); p.x = abs(p.x);
    if( sc.y*p.x > sc.x*p.y )
    {
        float2  w = p - ra*sc;
        float d = length(w);
        return float3( d-rb, float2(s*w.x,w.y)/d );
    }
    else
    {
        float l = length(q);
        float w = l - ra;
        return float3( abs(w)-rb, sign(w)*q/l );
    }
}

float3 sdgSegment( in float2 p, in float2 a, in float2 b, in float r )
{
    float2 ba = b-a, pa = p-a;
    float h = clamp( dot(pa,ba)/dot(ba,ba), 0.0, 1.0 );
    float2  q = pa-h*ba;
    float d = length(q);
    return float3(d-r,q/d);
}

float3 sdgVesica( float2 p, float r, float d )
{
    float2 s = sign(p); p = abs(p);
    float b = sqrt(r*r-d*d);
    if( (p.y-b)*d>p.x*b )
    {
        float2  q = float2(p.x,p.y-b);
        float l = length(q)*sign(d);
        return float3( l, s*q/l );
    }
    else
    {
        float2  q = float2(p.x+d,p.y);
        float l = length(q);
        return float3( l-r, s*q/l );
    }
}

float3 sdgBox( in float2 p, in float2 b )
{
    float2 w = abs(p)-b;
    float2 s = float2(p.x<0.0?-1:1,p.y<0.0?-1:1);
    float g = max(w.x,w.y);
    float2  q = max(w,0.0);
    float l = length(q);
    return float3(   (g>0.0)?l  :g,
                s*((g>0.0)?q/l:((w.x>w.y)?float2(1,0):float2(0,1))));
}

float3 sdgCross( in float2 p, in float2 b ) 
{
    float2 s = sign(p); p = abs(p); 
    float2  q = ((p.y>p.x)?p.yx:p.xy) - b;
    float h = max( q.x, q.y );
    float2  o = max( (h<0.0)?float2(b.y-b.x,0.0)-q:q, 0.0 );
    float l = length(o);
    float3  r = (h<0.0 && -q.x<l)?float3(-q.x,1.0,0.0):float3(l,o/l);
    return float3( sign(h)*r.x, s*((p.y>p.x)?r.zy:r.yz) );
}

float3 sdgPentagon( in float2 p, in float r ) 
{
    const float3 m = float3(0.80901699,0.58778525,0.72654253);
    const float2 n = float2(m.x*m.x-m.y*m.y,2.0*m.x*m.y );
    float s = sign(p.x);
    p.x = abs(p.x);
    float w1 = p.x*m.x + p.y*m.y;
    float w2 = p.x*n.x - p.y*n.y;
    p -= 2.0*max(w1,0.0)*float2(m.x,m.y);
    p -= 2.0*min(w2,0.0)*float2(m.x,-m.y);
    p -= float2(clamp(p.x,-r*m.z,r*m.z),-r);
    float d = length(p)*sign(-p.y);
    float2 g = (w2<0.0)?mul(float2x2(-m.x,m.y,-m.y,-m.x),p):
             (w1>0.0)?mul(float2x2(-n.x,-n.y,-n.y,n.x),p):
             p;
    g.x *= s;
    return float3(d, g/d );
}

float3 sdgHexagon( in float2 p, in float r ) 
{
    const float3 k = float3(-0.866025404,0.5,0.577350269);
    float2 s = sign(p); p = abs(p);
    float w = dot(k.xy,p);    
    p -= 2.0*min(w,0.0)*k.xy;
    p -= float2(clamp(p.x, -k.z*r, k.z*r), r);
    float d = length(p)*sign(p.y);
    float2  g = (w<0.0) ? mul(float2x2(-k.y,-k.x,-k.x,k.y),p) : p;
    return float3( d, s*g/d );
}

float3 sdgTriangleIsosceles( in float2 p, in float2 q )
{
    float w = sign(p.x);
    p.x = abs(p.x);
    float2 a = p - q*clamp( dot(p,q)/dot(q,q), 0.0, 1.0 );
    float2 b = p - q*float2( clamp( p.x/q.x, 0.0, 1.0 ), 1.0 );
    float k = sign(q.y);
    float l1 = dot(a,a);
    float l2 = dot(b,b);
    float d = sqrt((l1<l2)?l1:l2);
    float2  g =      (l1<l2)? a: b;
    float s = max( k*(p.x*q.y-p.y*q.x),k*(p.y-q.y)  );
    return float3(d,float2(w*g.x,g.y)/d)*sign(s);
}


float3 sdgTriangle( in float2 p, in float2 v[3] )
{
    float gs = cro(v[0]-v[2],v[1]-v[0]);
    float4 res;
    
    {
    float2  e = v[1]-v[0], w = p-v[0];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4(d,q,s);
    } {
    float2  e = v[2]-v[1], w = p-v[1];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4( (d<res.x) ? float3(d,q) : res.xyz,
                (s>res.w) ?      s    : res.w );
    } {
    float2  e = v[0]-v[2], w = p-v[2];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4( (d<res.x) ? float3(d,q) : res.xyz,
                (s>res.w) ?      s    : res.w );
    }
    
    float d = sqrt(res.x)*sign(res.w);
    return float3(d,res.yz/d);
}


float3 sdgQuad( in float2 p, in float2 v[4] )
{
    float gs = cro(v[0]-v[3],v[1]-v[0]);
    float4 res;
    
    {
    float2  e = v[1]-v[0], w = p-v[0];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4(d,q,s);
    } {
    float2  e = v[2]-v[1], w = p-v[1];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4( (d<res.x) ? float3(d,q) : res.xyz,
                (s>res.w) ?      s    : res.w );
    } {
    float2  e = v[3]-v[2], w = p-v[2];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4( (d<res.x) ? float3(d,q) : res.xyz,
                (s>res.w) ?      s    : res.w );
    } {
    float2  e = v[0]-v[3], w = p-v[3];
    float2  q = w-e*clamp(dot(w,e)/dot(e,e),0.0,1.0);
    float d = dot(q,q), s = gs*cro(w,e);
    res = float4( (d<res.x) ? float3(d,q) : res.xyz,
                (s>res.w) ?      s    : res.w );
    }    
    
    float d = sqrt(res.x)*sign(res.w);
    return float3(d,res.yz/d);
}

float3 sdgEllipse( float2 p, in float2 ab )
{
    float2 sp = sign(p); p = abs( p );
    
    bool s = dot(p/ab,p/ab)>1.0;
    float w = atan2(p.y*ab.x, p.x*ab.y);
    if(!s) w=(ab.x*(p.x-ab.x)<ab.y*(p.y-ab.y))? 1.570796327 : 0.0;
    
    for( int i=0; i<4; i++ )
    {
        float2 cs = float2(cos(w),sin(w));
        float2 u = ab*float2( cs.x,cs.y);
        float2 v = ab*float2(-cs.y,cs.x);
        w = w + dot(p-u,v)/(dot(p-u,u)+dot(v,v));
    }
    float2  q = ab*float2(cos(w),sin(w));

    float d = length(p-q);
    return float3( d, sp*(p-q)/d ) * (s?1.0:-1.0);
}

float3 sdMoon(float2 p, float d, float ra, float rb )
{
    float s = sign(p.y);
    p.y = abs(p.y);

    float a = (ra*ra - rb*rb + d*d)/(2.0*d);
    float b = sqrt(max(ra*ra-a*a,0.0));
    if( d*(p.x*b-p.y*a) > d*d*max(b-p.y,0.0) )
    {
        float2 w = p-float2(a,b); float d = length(w); w.y *= s;
        return float3(d,w/d);
    }

    float2 w1 = p;
    float2 w2 = p-float2(d,0);
    float l1 = length(w1); float d1 = l1-ra; w1.y *= s;
    float l2 = length(w2); float d2 = rb-l2; w2.y *= s;
    
    return (d1>d2) ? float3(d1,w1/l1) : float3(d2,-w2/l2);
}

float3 sdgParabola( in float2 pos, in float k )
{
    float s = sign(pos.x);
    pos.x = abs(pos.x);

    float ik = 1.0/k;
    float p = ik*(pos.y - 0.5*ik)/3.0;
    float q = 0.25*ik*ik*pos.x;
    float h = q*q - p*p*p;
    float r = sqrt(abs(h));

    float x = (h>0.0) ? 
        pow(q+r,1.0/3.0) - pow(abs(q-r),1.0/3.0)*sign(r-q) :
        2.0*cos(atan2(r,q)/3.0)*sqrt(p);
    
    float z = sign(pos.x-x);
    float2 w = pos-float2(x,k*x*x); float l = length(w); w.x*=s;
    return z*float3(l, w/l );
}

float3 sdgTrapezoid( float2 p, float ra, float rb, float he, out float2 oc )
{
    float sx = (p.x<0.0)?-1.0:1.0;
    float sy = (p.y<0.0)?-1.0:1.0;

    p.x = abs(p.x);

    float4 re;
    {
        float h = min(p.x,(p.y<0.0)?ra:rb);
        float2  c = float2(h,sy*he);
        float2  q = p - c;
        float d = dot(q,q);
        float s = abs(p.y) - he;
        re = float4(d,q,s);
        oc = c;
    }
    {
        float2  k = float2(rb-ra,2.0*he);
        float2  w = p - float2(ra, -he);
        float h = clamp(dot(w,k)/dot(k,k),0.0,1.0);
        float2  c = float2(ra,-he) + h*k;
        float2  q = p - c;
        float d = dot(q,q);
        float s = w.x*k.y - w.y*k.x;
        if( d<re.x ) { oc = c; re.xyz = float3(d,q); }
        if( s>re.w ) { re.w = s; }
    }
   
    float d = sqrt(re.x)*sign(re.w);
    re.y *= sx;
    oc.x *= sx;
    
    return float3(d,re.yz/d);
}


float3 sdgHeart( in float2 p )
{
    float sx = (p.x<0.0)?-1.0:1.0;
    p.x = abs(p.x);
 
    if( p.y+p.x>1.0 )
    {
        const float r = sqrt(2.0)/4.0;
        float2 q0 = p - float2(0.25,0.75);
        float l = length(q0);
        float3 d = float3(l-r, q0/l);
        d.y *= sx;
        return d;
    }
    else
    {
        float2 q1 = p - float2(0.0,1.0);      
        float2 q2 = p - 0.5*max(p.x+p.y,0.0);
        float3 d1 = float3(dot(q1,q1),q1);
        float3 d2 = float3(dot(q2,q2),q2);
        float3 d = (d1.x<d2.x) ? d1: d2;
        d.x = sqrt(d.x);
        d.yz /= d.x;
        d *= (p.x>p.y)?1.0:-1.0;
        d.y *= sx;
        return d;
    }
}